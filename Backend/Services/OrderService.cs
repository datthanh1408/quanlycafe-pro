using CoffeeShop.Authorization;
using CoffeeShop.Data;
using CoffeeShop.Helpers;
using CoffeeShop.Hubs;
using CoffeeShop.Models.Entities;
using CoffeeShop.Models.Enums;
using CoffeeShop.Models.ViewModels;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShop.Services;

public interface IOrderService
{
    Task<(bool Success, Order? Order, string? ErrorMessage)> CreateOrderAsync(CreateOrderRequestDto request);
    Task<(bool Success, Order? Order, string? ErrorMessage)> AppendItemsToOpenTableOrderAsync(int orderId, List<OrderRequestLineDto> lines, string actorName);
    Task<(bool Success, string? ErrorMessage)> ChangeStatusAsync(int orderId, OrderStatus targetStatus, string actorName, bool isAdmin, string? note = null, byte[]? expectedRowVersion = null);
    Task<(bool Success, string? ErrorMessage)> ConfirmPaymentAsync(int orderId, string actorName);
    Task<(bool Success, string? ErrorMessage)> CancelOrderAsync(int orderId, string reason, string actorName, string? currentUserId, bool isAdmin, bool hasStaffCancelClaim);
    Task<(bool Success, string? ErrorMessage)> VoidAndRefundCompletedOrderAsync(int orderId, string reason, string adminName);
}

public class OrderService : IOrderService
{
    private static readonly SemaphoreSlim CodeGenLock = new(1, 1);

    private readonly AppDbContext _db;
    private readonly IPricingService _pricingService;
    private readonly IPromotionService _promotionService;
    private readonly IInventoryService _inventoryService;
    private readonly ISettingService _settingService;
    private readonly IAuditService _auditService;
    private readonly IHubContext<OrderHub> _hub;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        AppDbContext db,
        IPricingService pricingService,
        IPromotionService promotionService,
        IInventoryService inventoryService,
        ISettingService settingService,
        IAuditService auditService,
        IHubContext<OrderHub> hub,
        ILogger<OrderService> logger)
    {
        _db = db;
        _pricingService = pricingService;
        _promotionService = promotionService;
        _inventoryService = inventoryService;
        _settingService = settingService;
        _auditService = auditService;
        _hub = hub;
        _logger = logger;
    }

    private async Task<string> GenerateOrderCodeAsync(DateTime utcTimestamp)
    {
        var vnTime = FormatHelper.ToVnTime(utcTimestamp);
        var prefix = $"CF-{vnTime:yyMMdd}-";

        var lastCode = await _db.Orders
            .AsNoTracking()
            .Where(o => o.Code.StartsWith(prefix))
            .OrderByDescending(o => o.Code)
            .Select(o => o.Code)
            .FirstOrDefaultAsync();

        int nextNumber = 1;
        if (!string.IsNullOrEmpty(lastCode) && lastCode.Length > prefix.Length)
        {
            var tail = lastCode[prefix.Length..];
            if (int.TryParse(tail, out var parsed))
            {
                nextNumber = parsed + 1;
            }
        }

        return $"{prefix}{nextNumber:D4}";
    }

    public async Task<(bool Success, Order? Order, string? ErrorMessage)> CreateOrderAsync(CreateOrderRequestDto request)
    {
        if (request.Lines == null || request.Lines.Count == 0)
            return (false, null, "Đơn hàng phải có ít nhất 1 sản phẩm.");

        var pricedLines = await _pricingService.PriceLinesAsync(
            request.Lines.Select((l, idx) => ($"L{idx}", l)));

        var unavailable = pricedLines.Where(x => !x.IsAvailable).ToList();
        if (unavailable.Count > 0)
        {
            var names = string.Join(", ", unavailable.Select(u => u.ProductName));
            return (false, null, $"Không thể đặt món đang tạm hết hoặc ngừng kinh doanh: {names}.");
        }

        var subtotal = pricedLines.Sum(x => x.LineTotal);
        if (subtotal <= 0)
            return (false, null, "Tổng giá trị đơn hàng không hợp lệ.");

        decimal discount = 0m;
        int? promotionId = null;

        if (!string.IsNullOrWhiteSpace(request.PromoCode))
        {
            var promoRes = await _promotionService.ValidateAndCalculateAsync(request.PromoCode, subtotal, request.CustomerId);
            if (!promoRes.IsValid)
            {
                return (false, null, promoRes.Message);
            }
            discount = promoRes.DiscountAmount;
            promotionId = promoRes.PromotionId;
        }

        var shippingFee = await _pricingService.CalculateShippingFeeAsync(request.OrderType, subtotal - discount);
        var total = _pricingService.CalculateFinalTotal(subtotal, discount, shippingFee);

        var createdUtc = request.OverrideCreatedAtUtc ?? DateTime.UtcNow;

        await CodeGenLock.WaitAsync();
        try
        {
            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                CafeTable? table = null;
                if (request.TableId.HasValue)
                {
                    table = await _db.CafeTables.FirstOrDefaultAsync(t => t.Id == request.TableId.Value && !t.IsDeleted && t.IsActive);
                    if (table == null)
                        return (false, null, "Bàn đã chọn không tồn tại hoặc ngừng phục vụ.");

                    var tableHasOpen = await _db.Orders.AnyAsync(o =>
                        o.TableId == table.Id &&
                        o.Status != OrderStatus.Completed &&
                        o.Status != OrderStatus.Cancelled);

                    if (tableHasOpen)
                        return (false, null, $"Bàn {table.Code} hiện đang có đơn mở. Vui lòng chọn bàn trống khác hoặc thêm món vào đơn hiện tại.");
                }

                // Gắn Shift đang mở của nhân viên nếu có (P1)
                int? activeShiftId = null;
                if (!string.IsNullOrWhiteSpace(request.StaffId))
                {
                    activeShiftId = await _db.Shifts
                        .Where(s => s.StaffId == request.StaffId && s.ClosedAt == null)
                        .Select(s => (int?)s.Id)
                        .FirstOrDefaultAsync();
                }

                // Đơn tạo từ POS bắt đầu ở trạng thái "Đã xác nhận" (Mục 6.1)
                var initialStatus = request.Source == OrderSource.POS
                    ? OrderStatus.Confirmed
                    : OrderStatus.Pending;

                var code = await GenerateOrderCodeAsync(createdUtc);

                var order = new Order
                {
                    Code = code,
                    CustomerId = request.CustomerId,
                    StaffId = request.StaffId,
                    TableId = table?.Id,
                    OrderType = request.OrderType,
                    Source = request.Source,
                    Status = initialStatus,
                    PaymentMethod = string.IsNullOrWhiteSpace(request.PaymentMethod) ? "CASH" : request.PaymentMethod.ToUpperInvariant(),
                    PaymentStatus = request.MarkPaidImmediately ? PaymentStatus.Paid : PaymentStatus.Unpaid,
                    PaymentConfirmedBy = request.MarkPaidImmediately ? request.ActorDisplayName : null,
                    PaidAt = request.MarkPaidImmediately ? createdUtc : null,
                    Subtotal = subtotal,
                    DiscountAmount = discount,
                    ShippingFee = shippingFee,
                    Total = total,
                    PromotionId = promotionId,
                    CustomerName = string.IsNullOrWhiteSpace(request.CustomerName) ? "Khách lẻ" : FormatHelper.SanitizeNote(request.CustomerName, 120),
                    Phone = FormatHelper.SanitizeNote(request.Phone, 20),
                    DeliveryAddress = FormatHelper.SanitizeNote(request.DeliveryAddress, 300),
                    Note = FormatHelper.SanitizeNote(request.Note, 200),
                    ShiftId = activeShiftId,
                    CreatedAt = createdUtc,
                    UpdatedAt = createdUtc
                };

                foreach (var line in pricedLines)
                {
                    var orderItem = new OrderItem
                    {
                        ProductId = line.ProductId,
                        ProductName = line.ProductName,
                        SizeName = line.SizeName,
                        UnitPrice = line.UnitPrice,
                        Temperature = line.Temperature,
                        SugarLevel = line.SugarLevel,
                        IceLevel = line.IceLevel,
                        Quantity = line.Quantity,
                        Note = line.Note,
                        LineTotal = line.LineTotal
                    };

                    foreach (var top in line.Toppings)
                    {
                        orderItem.Toppings.Add(new OrderItemTopping
                        {
                            ToppingName = top.Name,
                            Price = top.Price
                        });
                    }

                    order.Items.Add(orderItem);
                }

                order.StatusHistories.Add(new OrderStatusHistory
                {
                    FromStatus = initialStatus,
                    ToStatus = initialStatus,
                    ChangedBy = request.ActorDisplayName,
                    ChangedAt = createdUtc,
                    Note = request.Source == OrderSource.POS ? "Tạo đơn tại quầy POS" : "Khách đặt đơn trực tuyến"
                });

                _db.Orders.Add(order);
                await _db.SaveChangesAsync();

                // Tăng UsedCount của khuyến mãi trong cùng transaction (Mục 6.5)
                if (promotionId.HasValue)
                {
                    var promoEntity = await _db.Promotions.FirstAsync(p => p.Id == promotionId.Value);
                    promoEntity.UsedCount += 1;

                    _db.PromotionUsages.Add(new PromotionUsage
                    {
                        PromotionId = promoEntity.Id,
                        UserId = request.CustomerId,
                        OrderId = order.Id,
                        UsedAt = createdUtc
                    });
                }

                // Cập nhật bàn sang "Đang sử dụng" (Mục 6.7)
                if (table != null)
                {
                    table.Status = TableStatus.Occupied;
                }

                await _db.SaveChangesAsync();
                await tx.CommitAsync();

                // Đẩy thông báo real-time qua SignalR (không làm hỏng luồng nếu đang chạy Seeder nền)
                if (!request.OverrideCreatedAtUtc.HasValue)
                {
                    await BroadcastNewOrderAsync(order, table?.Code);
                }

                return (true, order, null);
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "Lỗi khi tạo đơn hàng.");
                return (false, null, "Không thể tạo đơn hàng do lỗi xử lý dữ liệu. Vui lòng thử lại.");
            }
        }
        finally
        {
            CodeGenLock.Release();
        }
    }

    public async Task<(bool Success, Order? Order, string? ErrorMessage)> AppendItemsToOpenTableOrderAsync(
        int orderId,
        List<OrderRequestLineDto> lines,
        string actorName)
    {
        if (lines == null || lines.Count == 0)
            return (false, null, "Vui lòng chọn ít nhất 1 món để thêm vào bàn.");

        var priced = await _pricingService.PriceLinesAsync(lines.Select((l, i) => ($"A{i}", l)));
        if (priced.Any(x => !x.IsAvailable))
            return (false, null, "Có món đang tạm hết trong danh sách chọn thêm.");

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var order = await _db.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null)
                return (false, null, "Không tìm thấy đơn hàng tại bàn.");

            if (order.Status is OrderStatus.Completed or OrderStatus.Cancelled || order.PaymentStatus == PaymentStatus.Paid)
                return (false, null, "Đơn tại bàn đã thanh toán hoặc đã đóng, không thể thêm món.");

            decimal addedSubtotal = 0m;
            var newOrderItems = new List<OrderItem>();

            foreach (var line in priced)
            {
                var oi = new OrderItem
                {
                    OrderId = order.Id,
                    ProductId = line.ProductId,
                    ProductName = line.ProductName,
                    SizeName = line.SizeName,
                    UnitPrice = line.UnitPrice,
                    Temperature = line.Temperature,
                    SugarLevel = line.SugarLevel,
                    IceLevel = line.IceLevel,
                    Quantity = line.Quantity,
                    Note = line.Note,
                    LineTotal = line.LineTotal
                };
                foreach (var top in line.Toppings)
                {
                    oi.Toppings.Add(new OrderItemTopping { ToppingName = top.Name, Price = top.Price });
                }
                order.Items.Add(oi);
                newOrderItems.Add(oi);
                addedSubtotal += line.LineTotal;
            }

            // Nếu đơn đã ở giai đoạn Đang pha chế hoặc Sẵn sàng nhận thì trừ kho cho phần món gọi thêm và đưa về Đang pha chế
            if (order.Status is OrderStatus.Brewing or OrderStatus.Ready)
            {
                var tempDeltaOrder = new Order
                {
                    Id = order.Id,
                    Code = order.Code + " (gọi thêm)",
                    UpdatedAt = DateTime.UtcNow,
                    Items = newOrderItems
                };

                // Kiểm tra và trừ kho trực tiếp cho các món gọi thêm
                var productIds = newOrderItems.Select(x => x.ProductId).Distinct().ToList();
                var recipes = await _db.Recipes.Where(r => productIds.Contains(r.ProductId)).ToListAsync();
                var needed = new Dictionary<int, decimal>();
                foreach (var item in newOrderItems)
                {
                    foreach (var r in recipes.Where(r => r.ProductId == item.ProductId &&
                        (string.IsNullOrWhiteSpace(r.SizeName) || string.Equals(r.SizeName, item.SizeName, StringComparison.OrdinalIgnoreCase))))
                    {
                        var qty = r.Quantity * item.Quantity;
                        needed[r.IngredientId] = needed.TryGetValue(r.IngredientId, out var c) ? c + qty : qty;
                    }
                }

                if (needed.Count > 0)
                {
                    var ings = await _db.Ingredients.Where(i => needed.Keys.Contains(i.Id)).ToDictionaryAsync(i => i.Id);
                    foreach (var (ingId, needQty) in needed)
                    {
                        if (!ings.TryGetValue(ingId, out var ing) || ing.StockQty < needQty)
                        {
                            await tx.RollbackAsync();
                            return (false, null, $"Không đủ nguyên liệu cho món gọi thêm ({ing?.Name ?? "#"}).");
                        }
                    }
                    foreach (var (ingId, needQty) in needed)
                    {
                        var ing = ings[ingId];
                        ing.StockQty -= needQty;
                        _db.StockTransactions.Add(new StockTransaction
                        {
                            IngredientId = ing.Id,
                            Type = StockTransactionType.Sale,
                            Quantity = -needQty,
                            BalanceAfter = ing.StockQty,
                            Reason = $"Trừ kho món gọi thêm đơn {order.Code}",
                            OrderId = order.Id,
                            CreatedBy = actorName,
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }

                order.Status = OrderStatus.Brewing;
            }

            order.Subtotal += addedSubtotal;
            order.Total = _pricingService.CalculateFinalTotal(order.Subtotal, order.DiscountAmount, order.ShippingFee);
            order.UpdatedAt = DateTime.UtcNow;

            _db.OrderStatusHistories.Add(new OrderStatusHistory
            {
                OrderId = order.Id,
                FromStatus = order.Status,
                ToStatus = order.Status,
                ChangedBy = actorName,
                ChangedAt = DateTime.UtcNow,
                Note = $"Khách gọi thêm {priced.Sum(x => x.Quantity)} món (+{FormatHelper.Vnd(addedSubtotal)})"
            });

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            await BroadcastStatusChangeAsync(order);
            return (true, order, null);
        }
        catch (DbUpdateConcurrencyException)
        {
            await tx.RollbackAsync();
            return (false, null, "Đơn vừa được cập nhật bởi người khác. Vui lòng tải lại dữ liệu.");
        }
    }

    private static List<OrderStatus> GetFlowForOrderType(OrderType type)
    {
        return type == OrderType.Delivery
            ? new List<OrderStatus>
            {
                OrderStatus.Pending,
                OrderStatus.Confirmed,
                OrderStatus.Brewing,
                OrderStatus.Ready,
                OrderStatus.Delivering,
                OrderStatus.Completed
            }
            : new List<OrderStatus>
            {
                OrderStatus.Pending,
                OrderStatus.Confirmed,
                OrderStatus.Brewing,
                OrderStatus.Ready,
                OrderStatus.Completed
            };
    }

    public async Task<(bool Success, string? ErrorMessage)> ChangeStatusAsync(
        int orderId,
        OrderStatus targetStatus,
        string actorName,
        bool isAdmin,
        string? note = null,
        byte[]? expectedRowVersion = null)
    {
        if (targetStatus == OrderStatus.Cancelled)
            return (false, "Vui lòng sử dụng chức năng Hủy đơn có nhập lý do.");

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var order = await _db.Orders
                .Include(o => o.Items)
                .Include(o => o.Table)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null)
                return (false, "Không tìm thấy đơn hàng.");

            if (expectedRowVersion != null && expectedRowVersion.Length > 0 &&
                !order.RowVersion.SequenceEqual(expectedRowVersion))
            {
                return (false, "Đơn vừa được cập nhật bởi người khác. Vui lòng tải lại dữ liệu.");
            }

            if (order.Status is OrderStatus.Completed or OrderStatus.Cancelled)
                return (false, $"Đơn ở trạng thái '{order.Status.GetDisplayName()}' không thể chuyển trạng thái.");

            var flow = GetFlowForOrderType(order.OrderType);
            var currentIdx = flow.IndexOf(order.Status);
            var targetIdx = flow.IndexOf(targetStatus);

            if (currentIdx < 0 || targetIdx < 0)
                return (false, "Trạng thái đích không hợp lệ cho loại đơn hàng này.");

            bool isForwardOneStep = targetIdx == currentIdx + 1;
            bool isRollbackOneStep = isAdmin && targetIdx == currentIdx - 1;

            if (!isForwardOneStep && !isRollbackOneStep)
            {
                return (false, "Chỉ được phép chuyển tiến từng bước theo quy trình (Admin được lùi 1 bước kèm lý do).");
            }

            if (isRollbackOneStep && string.IsNullOrWhiteSpace(note))
            {
                return (false, "Admin lùi trạng thái đơn bắt buộc phải nhập lý do.");
            }

            // Ràng buộc thanh toán trước khi Hoàn thành (Mục 6.3)
            if (targetStatus == OrderStatus.Completed && order.PaymentStatus != PaymentStatus.Paid)
            {
                return (false, "Đơn hàng phải được xác nhận 'Đã thanh toán' trước khi chuyển sang 'Hoàn thành'.");
            }

            var fromStatus = order.Status;
            order.Status = targetStatus;
            order.UpdatedAt = DateTime.UtcNow;

            // Khi chuyển sang "Đang pha chế": tự động trừ kho theo công thức (Mục 6.6)
            if (targetStatus == OrderStatus.Brewing && isForwardOneStep)
            {
                var (deductOk, deductErr) = await _inventoryService.DeductStockForBrewingInCurrentTxAsync(order, actorName);
                if (!deductOk)
                {
                    await tx.RollbackAsync();
                    return (false, deductErr);
                }
            }

            // Khi Hoàn thành: giải phóng bàn về Trống + tăng SoldCount + cộng điểm tích lũy (P1)
            if (targetStatus == OrderStatus.Completed)
            {
                if (order.Table != null)
                {
                    order.Table.Status = TableStatus.Available;
                }

                var productIds = order.Items.Select(i => i.ProductId).Distinct().ToList();
                var products = await _db.Products.Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id);
                foreach (var item in order.Items)
                {
                    if (products.TryGetValue(item.ProductId, out var prod))
                    {
                        prod.SoldCount += item.Quantity;
                    }
                }

                if (!string.IsNullOrWhiteSpace(order.CustomerId))
                {
                    var settings = await _settingService.GetStoreSettingsAsync();
                    if (settings.LoyaltyEarnPerVnd > 0)
                    {
                        var earnedPoints = (int)(order.Total / settings.LoyaltyEarnPerVnd);
                        if (earnedPoints > 0)
                        {
                            var customer = await _db.Users.FirstOrDefaultAsync(u => u.Id == order.CustomerId);
                            if (customer != null)
                            {
                                customer.LoyaltyPoints += earnedPoints;
                            }
                        }
                    }
                }
            }

            _db.OrderStatusHistories.Add(new OrderStatusHistory
            {
                OrderId = order.Id,
                FromStatus = fromStatus,
                ToStatus = targetStatus,
                ChangedBy = actorName,
                ChangedAt = order.UpdatedAt,
                Note = FormatHelper.SanitizeNote(note)
            });

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            await BroadcastStatusChangeAsync(order);
            return (true, null);
        }
        catch (DbUpdateConcurrencyException)
        {
            await tx.RollbackAsync();
            return (false, "Đơn vừa được cập nhật bởi người khác. Vui lòng tải lại dữ liệu.");
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> ConfirmPaymentAsync(int orderId, string actorName)
    {
        try
        {
            var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == orderId);
            if (order == null) return (false, "Không tìm thấy đơn hàng.");
            if (order.Status == OrderStatus.Cancelled)
                return (false, "Không thể xác nhận thu tiền cho đơn đã hủy.");
            if (order.PaymentStatus == PaymentStatus.Paid)
                return (true, null);

            order.PaymentStatus = PaymentStatus.Paid;
            order.PaymentConfirmedBy = actorName;
            order.PaidAt = DateTime.UtcNow;
            order.UpdatedAt = DateTime.UtcNow;

            _db.OrderStatusHistories.Add(new OrderStatusHistory
            {
                OrderId = order.Id,
                FromStatus = order.Status,
                ToStatus = order.Status,
                ChangedBy = actorName,
                ChangedAt = DateTime.UtcNow,
                Note = $"Xác nhận đã thu tiền ({order.PaymentMethod})"
            });

            await _db.SaveChangesAsync();
            await BroadcastStatusChangeAsync(order);
            return (true, null);
        }
        catch (DbUpdateConcurrencyException)
        {
            return (false, "Đơn vừa được cập nhật bởi người khác. Vui lòng tải lại.");
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> CancelOrderAsync(
        int orderId,
        string reason,
        string actorName,
        string? currentUserId,
        bool isAdmin,
        bool hasStaffCancelClaim)
    {
        var cleanReason = FormatHelper.SanitizeNote(reason);
        if (string.IsNullOrWhiteSpace(cleanReason))
            return (false, "Vui lòng nhập lý do hủy đơn (bắt buộc).");

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var order = await _db.Orders
                .Include(o => o.Table)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null)
                return (false, "Không tìm thấy đơn hàng.");

            if (order.Status == OrderStatus.Cancelled)
                return (false, "Đơn hàng này đã bị hủy trước đó.");

            if (order.Status == OrderStatus.Completed)
                return (false, "Đơn đã 'Hoàn thành' không thể hủy. Admin vui lòng dùng chức năng Hoàn tiền (Void).");

            // Kiểm tra quyền hủy đơn theo Mục 6.4
            if (!isAdmin)
            {
                if (hasStaffCancelClaim)
                {
                    if (order.Status is not (OrderStatus.Pending or OrderStatus.Confirmed))
                    {
                        return (false, "Nhân viên chỉ được phép hủy đơn trước giai đoạn 'Đang pha chế'. Vui lòng liên hệ Admin.");
                    }
                }
                else
                {
                    // Customer: chỉ hủy đơn của chính mình ở trạng thái Chờ xác nhận (chống IDOR)
                    if (string.IsNullOrWhiteSpace(currentUserId) || order.CustomerId != currentUserId)
                    {
                        return (false, "Bạn không có quyền hủy đơn hàng này.");
                    }
                    if (order.Status != OrderStatus.Pending)
                    {
                        return (false, "Bạn chỉ có thể hủy đơn khi đơn còn ở trạng thái 'Chờ xác nhận'.");
                    }
                }
            }

            var prevStatus = order.Status;
            var prevPayment = order.PaymentStatus;

            order.Status = OrderStatus.Cancelled;
            order.CancelReason = cleanReason;
            order.CancelledBy = actorName;
            order.CancelledAt = DateTime.UtcNow;
            order.UpdatedAt = DateTime.UtcNow;

            // Nếu đã thanh toán trước khi hủy -> chuyển sang Đã hoàn tiền (Mục 6.4)
            if (order.PaymentStatus == PaymentStatus.Paid)
            {
                order.PaymentStatus = PaymentStatus.Refunded;
            }

            // Hoàn lại lượt dùng mã giảm giá (Mục 6.4)
            if (order.PromotionId.HasValue)
            {
                var promo = await _db.Promotions.FirstOrDefaultAsync(p => p.Id == order.PromotionId.Value);
                if (promo != null && promo.UsedCount > 0)
                {
                    promo.UsedCount -= 1;
                }

                var usages = await _db.PromotionUsages.Where(u => u.OrderId == order.Id).ToListAsync();
                if (usages.Count > 0)
                {
                    _db.PromotionUsages.RemoveRange(usages);
                }
            }

            // Trả bàn về Trống (Mục 6.4)
            // Lưu ý Mục 6.4: Nguyên liệu đã trừ từ lúc Đang pha chế KHÔNG tự hoàn kho khi hủy
            if (order.Table != null)
            {
                order.Table.Status = TableStatus.Available;
            }

            _db.OrderStatusHistories.Add(new OrderStatusHistory
            {
                OrderId = order.Id,
                FromStatus = prevStatus,
                ToStatus = OrderStatus.Cancelled,
                ChangedBy = actorName,
                ChangedAt = DateTime.UtcNow,
                Note = $"Hủy đơn: {cleanReason}"
            });

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            await _auditService.LogAsync(
                action: "CancelOrder",
                entityName: "Order",
                entityId: order.Code,
                oldValues: new { Status = prevStatus.ToString(), PaymentStatus = prevPayment.ToString() },
                newValues: new { Status = "Cancelled", Reason = cleanReason });

            await BroadcastStatusChangeAsync(order);
            return (true, null);
        }
        catch (DbUpdateConcurrencyException)
        {
            await tx.RollbackAsync();
            return (false, "Đơn vừa được cập nhật bởi người khác. Vui lòng tải lại.");
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> VoidAndRefundCompletedOrderAsync(int orderId, string reason, string adminName)
    {
        var cleanReason = FormatHelper.SanitizeNote(reason);
        if (string.IsNullOrWhiteSpace(cleanReason))
            return (false, "Vui lòng nhập lý do hoàn tiền / void đơn.");

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == orderId);
            if (order == null) return (false, "Không tìm thấy đơn hàng.");

            if (order.Status != OrderStatus.Completed)
                return (false, "Chỉ hoàn tiền (void) cho đơn đã ở trạng thái 'Hoàn thành'.");

            if (order.PaymentStatus == PaymentStatus.Refunded)
                return (false, "Đơn hàng này đã được hoàn tiền trước đó.");

            order.PaymentStatus = PaymentStatus.Refunded;
            order.CancelReason = cleanReason;
            order.CancelledBy = adminName;
            order.CancelledAt = DateTime.UtcNow;
            order.UpdatedAt = DateTime.UtcNow;

            // Trừ lại điểm tích lũy đã cộng khi hoàn tiền (Mục 6.9)
            if (!string.IsNullOrWhiteSpace(order.CustomerId))
            {
                var settings = await _settingService.GetStoreSettingsAsync();
                if (settings.LoyaltyEarnPerVnd > 0)
                {
                    var pointsToDeduct = (int)(order.Total / settings.LoyaltyEarnPerVnd);
                    if (pointsToDeduct > 0)
                    {
                        var customer = await _db.Users.FirstOrDefaultAsync(u => u.Id == order.CustomerId);
                        if (customer != null)
                        {
                            customer.LoyaltyPoints = Math.Max(0, customer.LoyaltyPoints - pointsToDeduct);
                        }
                    }
                }
            }

            _db.OrderStatusHistories.Add(new OrderStatusHistory
            {
                OrderId = order.Id,
                FromStatus = OrderStatus.Completed,
                ToStatus = OrderStatus.Completed,
                ChangedBy = adminName,
                ChangedAt = DateTime.UtcNow,
                Note = $"Hoàn tiền (Void): {cleanReason}"
            });

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            await _auditService.LogAsync(
                action: "RefundOrder",
                entityName: "Order",
                entityId: order.Code,
                oldValues: new { PaymentStatus = "Paid" },
                newValues: new { PaymentStatus = "Refunded", Reason = cleanReason });

            await BroadcastStatusChangeAsync(order);
            return (true, null);
        }
        catch (DbUpdateConcurrencyException)
        {
            await tx.RollbackAsync();
            return (false, "Đơn vừa được cập nhật bởi người khác.");
        }
    }

    private async Task BroadcastNewOrderAsync(Order order, string? tableCode)
    {
        try
        {
            var payload = new
            {
                id = order.Id,
                code = order.Code,
                customerName = order.CustomerName,
                orderType = order.OrderType.GetDisplayName(),
                tableCode = tableCode,
                totalFormatted = FormatHelper.Vnd(order.Total),
                status = order.Status.GetDisplayName(),
                statusColor = order.Status.GetBadgeColor(),
                createdAt = FormatHelper.DateTimeVn(order.CreatedAt, "HH:mm:ss")
            };

            await _hub.Clients.Group(OrderHub.StaffGroup).SendAsync("NewOrderReceived", payload);
            if (order.TableId.HasValue)
            {
                await _hub.Clients.All.SendAsync("TableStatusChanged", new { tableId = order.TableId.Value });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không gửi được sự kiện SignalR NewOrderReceived.");
        }
    }

    private async Task BroadcastStatusChangeAsync(Order order)
    {
        try
        {
            var payload = new
            {
                id = order.Id,
                code = order.Code,
                statusValue = (int)order.Status,
                statusText = order.Status.GetDisplayName(),
                statusColor = order.Status.GetBadgeColor(),
                paymentStatusText = order.PaymentStatus.GetDisplayName(),
                isPaid = order.PaymentStatus == PaymentStatus.Paid,
                updatedAt = FormatHelper.DateTimeVn(order.UpdatedAt, "dd/MM/yyyy HH:mm:ss")
            };

            await _hub.Clients.Group(OrderHub.StaffGroup).SendAsync("OrderStatusChanged", payload);
            await _hub.Clients.Group($"Order_{order.Code}").SendAsync("OrderStatusChanged", payload);
            if (order.TableId.HasValue)
            {
                await _hub.Clients.All.SendAsync("TableStatusChanged", new { tableId = order.TableId.Value });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không gửi được sự kiện SignalR OrderStatusChanged.");
        }
    }
}

