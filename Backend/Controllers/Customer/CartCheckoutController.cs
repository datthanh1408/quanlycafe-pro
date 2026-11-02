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

namespace CoffeeShop.Controllers;

public class CartCheckoutController : Controller
{
    private const string SubmittedTokenKey = "LastCheckoutIdempotencyToken";

    private readonly AppDbContext _db;
    private readonly ICartService _cartService;
    private readonly IOrderService _orderService;
    private readonly ISettingService _settingService;
    private readonly ITableService _tableService;
    private readonly UserManager<ApplicationUser> _userManager;

    public CartCheckoutController(
        AppDbContext db,
        ICartService cartService,
        IOrderService orderService,
        ISettingService settingService,
        ITableService tableService,
        UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _cartService = cartService;
        _orderService = orderService;
        _settingService = settingService;
        _tableService = tableService;
        _userManager = userManager;
    }

    private string? CurrentUserId => _userManager.GetUserId(User);

    [HttpGet("/cart")]
    [AllowAnonymous]
    public async Task<IActionResult> Cart()
    {
        var summary = await _cartService.GetCartSummaryAsync(CurrentUserId);
        return View(summary);
    }

    [HttpPost("/cart/add")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddToCart(
        int productId,
        string? sizeName,
        string? temperature,
        string? sugarLevel,
        string? iceLevel,
        List<int>? toppingIds,
        int quantity = 1,
        string? note = null,
        string? returnUrl = null)
    {
        if (User.IsInRole(AppRoles.Staff) || User.IsInRole(AppRoles.Admin))
        {
            TempData["ToastWarning"] = "Tài khoản Nhân viên/Admin vui lòng tạo đơn tại màn hình Quầy POS.";
            return Redirect("/staff/pos");
        }

        var prod = await _db.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Id == productId && !p.IsDeleted);

        if (prod == null || !prod.IsAvailable || !prod.Category.IsActive || prod.Category.IsDeleted)
        {
            TempData["ToastError"] = "Món này hiện đang tạm hết hoặc ngừng phục vụ.";
            return Redirect("/menu");
        }

        _cartService.AddItem(new OrderRequestLineDto
        {
            ProductId = productId,
            SizeName = sizeName,
            Temperature = temperature,
            SugarLevel = sugarLevel,
            IceLevel = iceLevel,
            ToppingIds = toppingIds ?? new List<int>(),
            Quantity = Math.Clamp(quantity, 1, 99),
            Note = note
        });

        TempData["ToastSuccess"] = $"Đã thêm \"{prod.Name}\" vào giỏ hàng!";

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return LocalRedirect(returnUrl);

        return RedirectToAction(nameof(Cart));
    }

    [HttpPost("/cart/update")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateQuantity(string lineId, int quantity)
    {
        _cartService.UpdateQuantity(lineId, quantity);
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            var summary = await _cartService.GetCartSummaryAsync(CurrentUserId);
            return Json(new
            {
                success = true,
                totalQuantity = summary.TotalQuantity,
                subtotal = Helpers.FormatHelper.Vnd(summary.Subtotal),
                discount = Helpers.FormatHelper.Vnd(summary.DiscountAmount),
                total = Helpers.FormatHelper.Vnd(summary.Total)
            });
        }
        return RedirectToAction(nameof(Cart));
    }

    [HttpPost("/cart/remove")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public IActionResult RemoveLine(string lineId)
    {
        _cartService.RemoveItem(lineId);
        TempData["ToastSuccess"] = "Đã xóa món khỏi giỏ hàng.";
        return RedirectToAction(nameof(Cart));
    }

    [HttpPost("/cart/apply-promo")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApplyPromo(string? promoCode)
    {
        _cartService.SetPromoCode(promoCode);
        var summary = await _cartService.GetCartSummaryAsync(CurrentUserId);
        if (!string.IsNullOrWhiteSpace(promoCode))
        {
            if (summary.PromoValid)
                TempData["ToastSuccess"] = summary.PromoMessage;
            else
            {
                _cartService.SetPromoCode(null);
                TempData["ToastError"] = summary.PromoMessage;
            }
        }
        else
        {
            TempData["ToastSuccess"] = "Đã gỡ mã giảm giá.";
        }
        return RedirectToAction(nameof(Cart));
    }

    [HttpGet("/checkout")]
    [Authorize(Roles = AppRoles.Customer)]
    public async Task<IActionResult> Checkout(OrderType orderType = OrderType.TakeAway)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Redirect("/login?returnUrl=/checkout");

        var cart = await _cartService.GetCartSummaryAsync(user.Id, orderType);
        if (cart.Items.Count == 0)
        {
            TempData["ToastWarning"] = "Giỏ hàng của bạn đang trống.";
            return Redirect("/menu");
        }

        var store = await _settingService.GetStoreSettingsAsync();
        var methods = await _settingService.GetActivePaymentMethodsAsync();
        var tables = await _tableService.GetTablesFor3DAsync();
        foreach (var t in tables) { t.ActiveOrderCode = null; t.ActiveOrderTotal = null; }

        return View(new CheckoutPageVm
        {
            Cart = cart,
            Store = store,
            PaymentMethods = methods,
            Tables3D = tables,
            Form = new CheckoutFormVm
            {
                CustomerName = user.FullName,
                Phone = user.Phone ?? "",
                DeliveryAddress = user.Address,
                OrderType = orderType,
                PaymentMethod = methods.FirstOrDefault()?.Code ?? "CASH",
                PromoCode = cart.PromoValid ? cart.PromoCode : null,
                IdempotencyToken = Guid.NewGuid().ToString("N")
            }
        });
    }

    [HttpPost("/checkout")]
    [Authorize(Roles = AppRoles.Customer)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PlaceOrder([Bind(Prefix = "Form")] CheckoutFormVm form)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Redirect("/login");

        // Chống bấm đặt hàng 2 lần liên tiếp (Mục 7.1)
        var lastToken = HttpContext.Session.GetString(SubmittedTokenKey);
        if (!string.IsNullOrWhiteSpace(form.IdempotencyToken) && lastToken == form.IdempotencyToken)
        {
            return Redirect("/orders");
        }

        var rawItems = _cartService.GetRawItems();
        if (rawItems.Count == 0)
        {
            TempData["ToastWarning"] = "Giỏ hàng của bạn đang trống.";
            return Redirect("/menu");
        }

        if (form.OrderType == OrderType.Delivery && string.IsNullOrWhiteSpace(form.DeliveryAddress))
        {
            ModelState.AddModelError("Form.DeliveryAddress", "Vui lòng nhập địa chỉ nhận giao hàng.");
        }

        var activeMethods = await _settingService.GetActivePaymentMethodsAsync();
        if (!activeMethods.Any(m => string.Equals(m.Code, form.PaymentMethod, StringComparison.OrdinalIgnoreCase)))
        {
            ModelState.AddModelError("Form.PaymentMethod", "Phương thức thanh toán đã chọn hiện không khả dụng.");
        }

        if (!ModelState.IsValid)
        {
            var tables = await _tableService.GetTablesFor3DAsync();
            foreach (var t in tables) { t.ActiveOrderCode = null; t.ActiveOrderTotal = null; }
            return View("Checkout", new CheckoutPageVm
            {
                Form = form,
                Cart = await _cartService.GetCartSummaryAsync(user.Id, form.OrderType),
                Store = await _settingService.GetStoreSettingsAsync(),
                PaymentMethods = activeMethods,
                Tables3D = tables
            });
        }

        var createReq = new CreateOrderRequestDto
        {
            CustomerId = user.Id,
            ActorDisplayName = user.FullName,
            TableId = form.OrderType == OrderType.DineIn ? form.TableId : null,
            OrderType = form.OrderType,
            Source = OrderSource.Online,
            PaymentMethod = form.PaymentMethod,
            MarkPaidImmediately = false,
            CustomerName = form.CustomerName,
            Phone = form.Phone,
            DeliveryAddress = form.OrderType == OrderType.Delivery ? form.DeliveryAddress : null,
            PromoCode = !string.IsNullOrWhiteSpace(form.PromoCode) ? form.PromoCode : _cartService.GetSavedPromoCode(),
            Note = form.Note,
            Lines = rawItems.Select(r => new OrderRequestLineDto
            {
                ProductId = r.ProductId,
                SizeName = r.SizeName,
                Temperature = r.Temperature,
                SugarLevel = r.SugarLevel,
                IceLevel = r.IceLevel,
                ToppingIds = r.ToppingIds,
                Quantity = r.Quantity,
                Note = r.Note
            }).ToList()
        };

        var (ok, createdOrder, err) = await _orderService.CreateOrderAsync(createReq);
        if (!ok || createdOrder == null)
        {
            TempData["ToastError"] = err ?? "Không thể đặt đơn hàng. Vui lòng kiểm tra lại giỏ hàng.";
            return RedirectToAction(nameof(Checkout), new { orderType = form.OrderType });
        }

        HttpContext.Session.SetString(SubmittedTokenKey, form.IdempotencyToken);
        _cartService.Clear();

        return RedirectToAction(nameof(Success), new { code = createdOrder.Code });
    }

    [HttpGet("/checkout/success/{code}")]
    [Authorize(Roles = AppRoles.Customer)]
    public async Task<IActionResult> Success(string code)
    {
        var userId = CurrentUserId;
        var order = await _db.Orders
            .AsNoTracking()
            .Include(o => o.Table)
            .FirstOrDefaultAsync(o => o.Code == code && o.CustomerId == userId);

        if (order == null)
            return Redirect("/orders");

        ViewBag.Store = await _settingService.GetStoreSettingsAsync();
        return View(order);
    }
}

