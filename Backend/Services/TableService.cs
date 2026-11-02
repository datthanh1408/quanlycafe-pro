using CoffeeShop.Data;
using CoffeeShop.Helpers;
using CoffeeShop.Hubs;
using CoffeeShop.Models.Entities;
using CoffeeShop.Models.Enums;
using CoffeeShop.Models.ViewModels;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShop.Services;

public interface ITableService
{
    Task<List<Table3DDto>> GetTablesFor3DAsync(string? zone = null);
    Task<Order?> GetOpenOrderByTableAsync(int tableId);
    Task<(bool Success, string? ErrorMessage)> ChangeTableStatusAsync(int tableId, TableStatus newStatus, string actorName);
    Task<(bool Success, string? ErrorMessage)> TransferTableAsync(int fromTableId, int toTableId, string actorName);
    Task<(bool Success, string? ErrorMessage)> MergeTablesAsync(int sourceTableId, int targetTableId, string actorName);
    Task<(bool Success, string? ErrorMessage)> UpdateTablePosition3DAsync(int tableId, double posX, double posY, string? zone);
}

public class TableService : ITableService
{
    private readonly AppDbContext _db;
    private readonly ISettingService _settingService;
    private readonly IHubContext<OrderHub> _hub;

    public TableService(AppDbContext db, ISettingService settingService, IHubContext<OrderHub> hub)
    {
        _db = db;
        _settingService = settingService;
        _hub = hub;
    }

    public async Task<List<Table3DDto>> GetTablesFor3DAsync(string? zone = null)
    {
        var settings = await _settingService.GetStoreSettingsAsync();
        var nowUtc = DateTime.UtcNow;
        var holdWindowEnd = nowUtc.AddMinutes(settings.ReservationHoldMinutes);

        var query = _db.CafeTables
            .AsNoTracking()
            .Where(t => !t.IsDeleted && t.IsActive);

        if (!string.IsNullOrWhiteSpace(zone))
        {
            query = query.Where(t => t.Zone == zone);
        }

        var tables = await query
            .OrderBy(t => t.Zone)
            .ThenBy(t => t.Code)
            .ToListAsync();

        var tableIds = tables.Select(t => t.Id).ToList();

        var openOrders = await _db.Orders
            .AsNoTracking()
            .Where(o => o.TableId.HasValue &&
                        tableIds.Contains(o.TableId.Value) &&
                        o.Status != OrderStatus.Completed &&
                        o.Status != OrderStatus.Cancelled)
            .ToListAsync();

        var upcomingReservations = await _db.TableReservations
            .AsNoTracking()
            .Where(r => tableIds.Contains(r.TableId) &&
                        r.Status == ReservationStatus.Confirmed &&
                        r.ReservedAt >= nowUtc.AddMinutes(-15) &&
                        r.ReservedAt <= holdWindowEnd)
            .OrderBy(r => r.ReservedAt)
            .ToListAsync();

        var result = new List<Table3DDto>();

        foreach (var t in tables)
        {
            var openOrder = openOrders.FirstOrDefault(o => o.TableId == t.Id);
            var nearRes = upcomingReservations.FirstOrDefault(r => r.TableId == t.Id);

            var effectiveStatus = t.Status;
            if (openOrder != null)
            {
                effectiveStatus = TableStatus.Occupied;
            }
            else if (nearRes != null && effectiveStatus == TableStatus.Available)
            {
                effectiveStatus = TableStatus.Reserved;
            }

            result.Add(new Table3DDto
            {
                Id = t.Id,
                Code = t.Code,
                Zone = t.Zone,
                Capacity = t.Capacity,
                Status = effectiveStatus,
                StatusText = effectiveStatus.GetDisplayName(),
                PosX = t.PosX,
                PosY = t.PosY,
                ActiveOrderId = openOrder?.Id,
                ActiveOrderCode = openOrder?.Code,
                ActiveOrderTotal = openOrder?.Total,
                ActiveOrderStatus = openOrder?.Status.GetDisplayName(),
                UpcomingReservationInfo = nearRes != null
                    ? $"{nearRes.CustomerName} ({FormatHelper.DateTimeVn(nearRes.ReservedAt, "HH:mm")})"
                    : null
            });
        }

        return result;
    }

    public async Task<Order?> GetOpenOrderByTableAsync(int tableId)
    {
        return await _db.Orders
            .Include(o => o.Items)
                .ThenInclude(i => i.Toppings)
            .Include(o => o.Table)
            .FirstOrDefaultAsync(o =>
                o.TableId == tableId &&
                o.Status != OrderStatus.Completed &&
                o.Status != OrderStatus.Cancelled);
    }

    public async Task<(bool Success, string? ErrorMessage)> ChangeTableStatusAsync(int tableId, TableStatus newStatus, string actorName)
    {
        var table = await _db.CafeTables.FirstOrDefaultAsync(t => t.Id == tableId && !t.IsDeleted);
        if (table == null) return (false, "Không tìm thấy bàn.");

        var openOrder = await GetOpenOrderByTableAsync(tableId);
        if (openOrder != null && newStatus != TableStatus.Occupied)
        {
            return (false, $"Bàn {table.Code} đang có đơn mở ({openOrder.Code}), không thể chuyển sang '{newStatus.GetDisplayName()}'.");
        }

        if (openOrder == null && newStatus == TableStatus.Occupied)
        {
            return (false, $"Bàn {table.Code} chưa có đơn mở. Hãy tạo đơn tại bàn để chuyển sang 'Đang sử dụng'.");
        }

        table.Status = newStatus;
        await _db.SaveChangesAsync();

        await _hub.Clients.All.SendAsync("TableStatusChanged", new
        {
            tableId = table.Id,
            code = table.Code,
            status = (int)table.Status,
            statusText = table.Status.GetDisplayName()
        });

        return (true, null);
    }

    public async Task<(bool Success, string? ErrorMessage)> TransferTableAsync(int fromTableId, int toTableId, string actorName)
    {
        if (fromTableId == toTableId)
            return (false, "Bàn đích phải khác bàn hiện tại.");

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var fromTable = await _db.CafeTables.FirstOrDefaultAsync(t => t.Id == fromTableId && !t.IsDeleted);
            var toTable = await _db.CafeTables.FirstOrDefaultAsync(t => t.Id == toTableId && !t.IsDeleted && t.IsActive);

            if (fromTable == null || toTable == null)
                return (false, "Không tìm thấy thông tin bàn.");

            var openOrder = await _db.Orders
                .FirstOrDefaultAsync(o =>
                    o.TableId == fromTableId &&
                    o.Status != OrderStatus.Completed &&
                    o.Status != OrderStatus.Cancelled);

            if (openOrder == null)
                return (false, $"Bàn {fromTable.Code} không có đơn mở để chuyển.");

            var destHasOpen = await _db.Orders
                .AnyAsync(o =>
                    o.TableId == toTableId &&
                    o.Status != OrderStatus.Completed &&
                    o.Status != OrderStatus.Cancelled);

            if (destHasOpen || toTable.Status == TableStatus.Occupied)
                return (false, $"Bàn đích {toTable.Code} đang có khách phục vụ. Hãy dùng chức năng Gộp bàn nếu muốn gộp đơn.");

            openOrder.TableId = toTable.Id;
            openOrder.UpdatedAt = DateTime.UtcNow;

            _db.OrderStatusHistories.Add(new OrderStatusHistory
            {
                OrderId = openOrder.Id,
                FromStatus = openOrder.Status,
                ToStatus = openOrder.Status,
                ChangedBy = actorName,
                ChangedAt = DateTime.UtcNow,
                Note = $"Chuyển bàn: {fromTable.Code} → {toTable.Code}"
            });

            fromTable.Status = TableStatus.Available;
            toTable.Status = TableStatus.Occupied;

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            await _hub.Clients.All.SendAsync("TableStatusChanged", new { transferred = true, fromTableId, toTableId });
            return (true, null);
        }
        catch (DbUpdateConcurrencyException)
        {
            await tx.RollbackAsync();
            return (false, "Trạng thái bàn vừa được cập nhật bởi nhân viên khác. Vui lòng tải lại.");
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> MergeTablesAsync(int sourceTableId, int targetTableId, string actorName)
    {
        if (sourceTableId == targetTableId)
            return (false, "Hai bàn cần gộp phải khác nhau.");

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var sourceTable = await _db.CafeTables.FirstOrDefaultAsync(t => t.Id == sourceTableId && !t.IsDeleted);
            var targetTable = await _db.CafeTables.FirstOrDefaultAsync(t => t.Id == targetTableId && !t.IsDeleted);

            if (sourceTable == null || targetTable == null)
                return (false, "Không tìm thấy bàn cần gộp.");

            var sourceOrder = await _db.Orders
                .Include(o => o.Items)
                    .ThenInclude(i => i.Toppings)
                .FirstOrDefaultAsync(o =>
                    o.TableId == sourceTableId &&
                    o.Status != OrderStatus.Completed &&
                    o.Status != OrderStatus.Cancelled);

            var targetOrder = await _db.Orders
                .Include(o => o.Items)
                    .ThenInclude(i => i.Toppings)
                .FirstOrDefaultAsync(o =>
                    o.TableId == targetTableId &&
                    o.Status != OrderStatus.Completed &&
                    o.Status != OrderStatus.Cancelled);

            if (sourceOrder == null || targetOrder == null)
                return (false, "Cả hai bàn đều phải đang có đơn mở để gộp bàn.");

            if (sourceOrder.PaymentStatus == PaymentStatus.Paid || targetOrder.PaymentStatus == PaymentStatus.Paid)
                return (false, "Không thể gộp đơn đã thanh toán.");

            foreach (var item in sourceOrder.Items)
            {
                var cloned = new OrderItem
                {
                    OrderId = targetOrder.Id,
                    ProductId = item.ProductId,
                    ProductName = item.ProductName,
                    SizeName = item.SizeName,
                    UnitPrice = item.UnitPrice,
                    Temperature = item.Temperature,
                    SugarLevel = item.SugarLevel,
                    IceLevel = item.IceLevel,
                    Quantity = item.Quantity,
                    Note = item.Note,
                    LineTotal = item.LineTotal
                };
                foreach (var top in item.Toppings)
                {
                    cloned.Toppings.Add(new OrderItemTopping
                    {
                        ToppingName = top.ToppingName,
                        Price = top.Price
                    });
                }
                targetOrder.Items.Add(cloned);
            }

            targetOrder.Subtotal += sourceOrder.Subtotal;
            targetOrder.Total = Math.Max(0m, targetOrder.Subtotal - targetOrder.DiscountAmount + targetOrder.ShippingFee);
            targetOrder.UpdatedAt = DateTime.UtcNow;

            sourceOrder.Status = OrderStatus.Cancelled;
            sourceOrder.CancelReason = $"Gộp đơn sang bàn {targetTable.Code} ({targetOrder.Code})";
            sourceOrder.CancelledBy = actorName;
            sourceOrder.CancelledAt = DateTime.UtcNow;
            sourceOrder.TableId = null;
            sourceOrder.UpdatedAt = DateTime.UtcNow;

            _db.OrderStatusHistories.Add(new OrderStatusHistory
            {
                OrderId = targetOrder.Id,
                FromStatus = targetOrder.Status,
                ToStatus = targetOrder.Status,
                ChangedBy = actorName,
                ChangedAt = DateTime.UtcNow,
                Note = $"Nhận gộp món từ bàn {sourceTable.Code} ({sourceOrder.Code})"
            });

            sourceTable.Status = TableStatus.Available;
            targetTable.Status = TableStatus.Occupied;

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            await _hub.Clients.All.SendAsync("TableStatusChanged", new { merged = true, sourceTableId, targetTableId });
            return (true, null);
        }
        catch (DbUpdateConcurrencyException)
        {
            await tx.RollbackAsync();
            return (false, "Dữ liệu bàn/đơn vừa thay đổi bởi người khác. Vui lòng thử lại.");
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> UpdateTablePosition3DAsync(int tableId, double posX, double posY, string? zone)
    {
        var table = await _db.CafeTables.FirstOrDefaultAsync(t => t.Id == tableId && !t.IsDeleted);
        if (table == null) return (false, "Không tìm thấy bàn.");

        table.PosX = Math.Round(Math.Clamp(posX, -12.0, 12.0), 2);
        table.PosY = Math.Round(Math.Clamp(posY, -12.0, 12.0), 2);
        if (!string.IsNullOrWhiteSpace(zone))
        {
            table.Zone = zone.Trim();
        }

        await _db.SaveChangesAsync();
        await _hub.Clients.All.SendAsync("TableStatusChanged", new { layoutUpdated = true, tableId = table.Id });
        return (true, null);
    }
}

