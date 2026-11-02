using CoffeeShop.Authorization;
using CoffeeShop.Data;
using CoffeeShop.Helpers;
using CoffeeShop.Models.Entities;
using CoffeeShop.Models.Enums;
using CoffeeShop.Models.ViewModels;
using CoffeeShop.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShop.Controllers;

[Authorize(Roles = AppRoles.Customer)]
public class CustomerPortalController : Controller
{
    private readonly AppDbContext _db;
    private readonly IOrderService _orderService;
    private readonly ICartService _cartService;
    private readonly ISettingService _settingService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IWebHostEnvironment _env;

    public CustomerPortalController(
        AppDbContext db,
        IOrderService orderService,
        ICartService cartService,
        ISettingService settingService,
        UserManager<ApplicationUser> userManager,
        IWebHostEnvironment env)
    {
        _db = db;
        _orderService = orderService;
        _cartService = cartService;
        _settingService = settingService;
        _userManager = userManager;
        _env = env;
    }

    [HttpGet("/orders")]
    public async Task<IActionResult> Orders(OrderStatus? status = null, int page = 1)
    {
        page = Math.Max(1, page);
        const int pageSize = 8;
        var userId = _userManager.GetUserId(User);

        var query = _db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .Include(o => o.Table)
            .Where(o => o.CustomerId == userId);

        if (status.HasValue)
        {
            query = query.Where(o => o.Status == status.Value);
        }

        var total = await query.CountAsync();
        var list = await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.FilterStatus = status;
        return View(new PagedResult<Order>
        {
            Items = list,
            Page = page,
            PageSize = pageSize,
            TotalItems = total
        });
    }

    [HttpGet("/orders/{code}")]
    public async Task<IActionResult> OrderDetail(string code)
    {
        var userId = _userManager.GetUserId(User);
        // Chống IDOR (Mục 5.3): chỉ lấy đơn thuộc về chính Customer đang đăng nhập
        var order = await _db.Orders
            .AsNoTracking()
            .Include(o => o.Table)
            .Include(o => o.Items)
                .ThenInclude(i => i.Toppings)
            .Include(o => o.StatusHistories)
            .FirstOrDefaultAsync(o => o.Code == code && o.CustomerId == userId);

        if (order == null)
        {
            return Redirect("/error/403");
        }

        ViewData["WatchOrderCode"] = order.Code;
        ViewBag.Store = await _settingService.GetStoreSettingsAsync();
        return View(order);
    }

    [HttpPost("/orders/{code}/cancel")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelMyOrder(string code, string reason)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Redirect("/login");

        var order = await _db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Code == code && o.CustomerId == user.Id);
        if (order == null)
            return Redirect("/error/403");

        var (ok, err) = await _orderService.CancelOrderAsync(
            orderId: order.Id,
            reason: reason,
            actorName: user.FullName,
            currentUserId: user.Id,
            isAdmin: false,
            hasStaffCancelClaim: false);

        if (ok)
            TempData["ToastSuccess"] = $"Đã hủy đơn hàng {code}.";
        else
            TempData["ToastError"] = err ?? "Không thể hủy đơn hàng.";

        return RedirectToAction(nameof(OrderDetail), new { code });
    }

    [HttpPost("/orders/{code}/reorder")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reorder(string code)
    {
        var userId = _userManager.GetUserId(User);
        var order = await _db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
                .ThenInclude(i => i.Toppings)
            .FirstOrDefaultAsync(o => o.Code == code && o.CustomerId == userId);

        if (order == null)
            return Redirect("/error/403");

        var allToppings = await _db.Toppings
            .AsNoTracking()
            .Where(t => !t.IsDeleted && t.IsActive)
            .ToListAsync();

        foreach (var item in order.Items)
        {
            var matchedTopIds = item.Toppings
                .Select(ot => allToppings.FirstOrDefault(t => t.Name == ot.ToppingName)?.Id)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .ToList();

            _cartService.AddItem(new OrderRequestLineDto
            {
                ProductId = item.ProductId,
                SizeName = item.SizeName,
                Temperature = item.Temperature,
                SugarLevel = item.SugarLevel,
                IceLevel = item.IceLevel,
                ToppingIds = matchedTopIds,
                Quantity = item.Quantity,
                Note = item.Note
            });
        }

        TempData["ToastSuccess"] = $"Đã thêm lại các món từ đơn {code} vào giỏ hàng!";
        return Redirect("/cart");
    }

    [HttpGet("/account")]
    public async Task<IActionResult> Account()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Redirect("/login");

        var recentOrders = await _db.Orders
            .AsNoTracking()
            .Where(o => o.CustomerId == user.Id)
            .OrderByDescending(o => o.CreatedAt)
            .Take(5)
            .ToListAsync();

        var totalSpent = await _db.Orders
            .Where(o => o.CustomerId == user.Id && o.Status == OrderStatus.Completed && o.PaymentStatus == PaymentStatus.Paid)
            .SumAsync(o => (decimal?)o.Total) ?? 0m;

        ViewBag.UserEntity = user;
        ViewBag.RecentOrders = recentOrders;
        ViewBag.TotalSpent = totalSpent;
        ViewBag.Store = await _settingService.GetStoreSettingsAsync();

        return View(new ProfileUpdateViewModel
        {
            FullName = user.FullName,
            Phone = user.Phone ?? "",
            Address = user.Address
        });
    }

    [HttpPost("/account")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Account(ProfileUpdateViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Redirect("/login");

        if (!ModelState.IsValid)
        {
            ViewBag.UserEntity = user;
            ViewBag.RecentOrders = await _db.Orders.AsNoTracking().Where(o => o.CustomerId == user.Id).OrderByDescending(o => o.CreatedAt).Take(5).ToListAsync();
            ViewBag.TotalSpent = await _db.Orders.Where(o => o.CustomerId == user.Id && o.Status == OrderStatus.Completed && o.PaymentStatus == PaymentStatus.Paid).SumAsync(o => (decimal?)o.Total) ?? 0m;
            ViewBag.Store = await _settingService.GetStoreSettingsAsync();
            return View(model);
        }

        if (model.AvatarFile != null && model.AvatarFile.Length > 0)
        {
            var (upOk, relUrl, upErr) = await ImageUploadHelper.SaveImageAsync(model.AvatarFile, _env, "avatars", user.AvatarUrl);
            if (!upOk)
            {
                TempData["ToastError"] = upErr;
                return RedirectToAction(nameof(Account));
            }
            user.AvatarUrl = relUrl;
        }

        user.FullName = model.FullName.Trim();
        user.Phone = model.Phone.Trim();
        user.PhoneNumber = model.Phone.Trim();
        user.Address = model.Address?.Trim();

        await _userManager.UpdateAsync(user);
        TempData["ToastSuccess"] = "Đã cập nhật hồ sơ cá nhân thành công!";
        return RedirectToAction(nameof(Account));
    }
}

