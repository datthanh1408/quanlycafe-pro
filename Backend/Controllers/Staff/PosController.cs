using System.Text.Json;
using CoffeeShop.Areas.Staff.Models;
using CoffeeShop.Authorization;
using CoffeeShop.Data;
using CoffeeShop.Models.Entities;
using CoffeeShop.Models.Enums;
using CoffeeShop.Models.ViewModels;
using CoffeeShop.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShop.Areas.Staff.Controllers;

[Area("Staff")]
[Authorize(Policy = AppPolicies.StaffOrAdmin)]
public class PosController : Controller
{
    private static readonly JsonSerializerOptions CamelJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly AppDbContext _db;
    private readonly IOrderService _orderService;
    private readonly ITableService _tableService;
    private readonly IPromotionService _promotionService;
    private readonly ISettingService _settingService;
    private readonly UserManager<ApplicationUser> _userManager;

    public PosController(
        AppDbContext db,
        IOrderService orderService,
        ITableService tableService,
        IPromotionService promotionService,
        ISettingService settingService,
        UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _orderService = orderService;
        _tableService = tableService;
        _promotionService = promotionService;
        _settingService = settingService;
        _userManager = userManager;
    }

    [HttpGet("/staff/pos")]
    public async Task<IActionResult> Index(int? tableId = null, string? customerName = null, string? phone = null)
    {
        var categories = await _db.Categories
            .AsNoTracking()
            .Where(c => !c.IsDeleted && c.IsActive)
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Name)
            .ToListAsync();

        var productsRaw = await _db.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Sizes)
            .Include(p => p.ProductToppings)
                .ThenInclude(pt => pt.Topping)
            .Where(p => !p.IsDeleted && !p.Category.IsDeleted && p.Category.IsActive)
            .OrderByDescending(p => p.IsAvailable)
            .ThenByDescending(p => p.IsFeatured)
            .ThenBy(p => p.Name)
            .ToListAsync();

        var clientProducts = productsRaw.Select(p => new PosProductClientDto
        {
            Id = p.Id,
            CategoryId = p.CategoryId,
            Name = p.Name,
            ImageUrl = p.ImageUrl,
            BasePrice = p.BasePrice,
            IsAvailable = p.IsAvailable,
            AllowTemperature = p.AllowTemperature,
            AllowSugar = p.AllowSugar,
            AllowIce = p.AllowIce,
            Sizes = p.Sizes
                .OrderBy(s => s.DisplayOrder)
                .ThenBy(s => s.Price)
                .Select(s => new PosSizeItemDto
                {
                    SizeName = s.SizeName,
                    Price = s.Price
                })
                .ToList(),
            Toppings = p.ProductToppings
                .Where(pt => pt.Topping != null && pt.Topping.IsActive && !pt.Topping.IsDeleted)
                .Select(pt => new PosToppingItemDto
                {
                    Id = pt.Topping.Id,
                    Name = pt.Topping.Name,
                    Price = pt.Topping.Price
                })
                .OrderBy(t => t.Price)
                .ThenBy(t => t.Name)
                .ToList()
        }).ToList();

        var tables = await _tableService.GetTablesFor3DAsync();
        var paymentMethods = await _settingService.GetActivePaymentMethodsAsync();
        var storeSettings = await _settingService.GetStoreSettingsAsync();
        bool canApplyPromo = AppPolicies.HasAdminOrClaim(User, StaffPermissions.ApplyPromotion);

        // Nếu mở POS từ một bàn đang có đơn mở và chưa truyền tên khách, tự động điền tên/SĐT khách của đơn đang mở
        string? prefillName = customerName;
        string? prefillPhone = phone;
        if (tableId.HasValue && string.IsNullOrWhiteSpace(prefillName))
        {
            var openOrder = await _tableService.GetOpenOrderByTableAsync(tableId.Value);
            if (openOrder != null)
            {
                prefillName = openOrder.CustomerName;
                prefillPhone = openOrder.Phone;
            }
        }

        var vm = new StaffPosVm
        {
            Categories = categories,
            Tables = tables,
            PaymentMethods = paymentMethods,
            StoreSettings = storeSettings,
            ProductsJson = JsonSerializer.Serialize(clientProducts, CamelJsonOptions),
            TablesJson = JsonSerializer.Serialize(tables, CamelJsonOptions),
            SelectedTableId = tableId,
            PrefillCustomerName = prefillName,
            PrefillPhone = prefillPhone,
            CanApplyPromotion = canApplyPromo
        };

        return View(vm);
    }

    [HttpPost("/staff/pos/validate-promo")]
    [Authorize(Policy = AppPolicies.CanApplyPromotion)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ValidatePromo([FromBody] PosValidatePromoDto? dto)
    {
        if (dto == null || string.IsNullOrWhiteSpace(dto.Code))
        {
            return Json(new PromotionValidationResult
            {
                IsValid = false,
                Message = "Vui lòng nhập mã khuyến mãi."
            });
        }

        var result = await _promotionService.ValidateAndCalculateAsync(dto.Code, dto.Subtotal, null);
        return Json(result);
    }

    [HttpPost("/staff/pos/submit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit([FromBody] PosSubmitOrderDto? dto)
    {
        if (dto == null || dto.Lines == null || dto.Lines.Count == 0)
        {
            return Json(new
            {
                success = false,
                errorMessage = "Đơn POS chưa có sản phẩm nào."
            });
        }

        var currentUser = await _userManager.GetUserAsync(User);
        var actorName = !string.IsNullOrWhiteSpace(currentUser?.FullName)
            ? currentUser.FullName
            : (User.Identity?.Name ?? "Thu ngân POS");

        // Nếu thêm món vào đơn bàn đang mở
        if (dto.AppendToOpenOrderId.HasValue && dto.AppendToOpenOrderId.Value > 0)
        {
            var appendRes = await _orderService.AppendItemsToOpenTableOrderAsync(
                dto.AppendToOpenOrderId.Value,
                dto.Lines,
                actorName);

            return Json(new
            {
                success = appendRes.Success,
                orderId = appendRes.Order?.Id,
                orderCode = appendRes.Order?.Code,
                errorMessage = appendRes.ErrorMessage
            });
        }

        // Kiểm tra quyền áp mã giảm giá phía server
        if (!string.IsNullOrWhiteSpace(dto.PromoCode) &&
            !AppPolicies.HasAdminOrClaim(User, StaffPermissions.ApplyPromotion))
        {
            return Json(new
            {
                success = false,
                errorMessage = "Tài khoản của bạn chưa được cấp quyền áp dụng mã khuyến mãi tại quầy POS."
            });
        }

        var normalizedOrderType = dto.OrderType == OrderType.TakeAway
            ? OrderType.TakeAway
            : OrderType.DineIn;

        // Tự động liên kết tài khoản khách hàng tích điểm nếu SĐT khớp khách hàng đang hoạt động
        string? matchedCustomerId = null;
        var cleanPhone = dto.Phone?.Trim();
        if (!string.IsNullOrWhiteSpace(cleanPhone))
        {
            var matchedCustomer = await _db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => !u.IsDeleted && u.IsActive &&
                                          (u.Phone == cleanPhone || u.PhoneNumber == cleanPhone));
            if (matchedCustomer != null)
            {
                matchedCustomerId = matchedCustomer.Id;
            }
        }

        var createReq = new CreateOrderRequestDto
        {
            CustomerId = matchedCustomerId,
            StaffId = currentUser?.Id,
            ActorDisplayName = actorName,
            TableId = normalizedOrderType == OrderType.DineIn ? dto.TableId : null,
            OrderType = normalizedOrderType,
            Source = OrderSource.POS,
            PaymentMethod = string.IsNullOrWhiteSpace(dto.PaymentMethod) ? "CASH" : dto.PaymentMethod,
            MarkPaidImmediately = dto.MarkPaidImmediately,
            CustomerName = string.IsNullOrWhiteSpace(dto.CustomerName) ? "Khách tại quầy" : dto.CustomerName.Trim(),
            Phone = cleanPhone,
            PromoCode = dto.PromoCode?.Trim(),
            Note = dto.Note,
            Lines = dto.Lines
        };

        var createRes = await _orderService.CreateOrderAsync(createReq);

        return Json(new
        {
            success = createRes.Success,
            orderId = createRes.Order?.Id,
            orderCode = createRes.Order?.Code,
            errorMessage = createRes.ErrorMessage
        });
    }
}
