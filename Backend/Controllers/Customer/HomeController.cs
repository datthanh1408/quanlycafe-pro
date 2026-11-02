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

[AllowAnonymous]
public class HomeController : Controller
{
    private readonly AppDbContext _db;
    private readonly ISettingService _settingService;
    private readonly ITableService _tableService;
    private readonly UserManager<ApplicationUser> _userManager;

    public HomeController(
        AppDbContext db,
        ISettingService settingService,
        ITableService tableService,
        UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _settingService = settingService;
        _tableService = tableService;
        _userManager = userManager;
    }

    [HttpGet("/")]
    public async Task<IActionResult> Index()
    {
        var store = await _settingService.GetStoreSettingsAsync();
        var categories = await _db.Categories
            .AsNoTracking()
            .Where(c => !c.IsDeleted && c.IsActive)
            .OrderBy(c => c.DisplayOrder)
            .ToListAsync();

        var featuredRaw = await _db.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Sizes)
            .Where(p => !p.IsDeleted && p.Category.IsActive && !p.Category.IsDeleted)
            .OrderByDescending(p => p.IsFeatured)
            .ThenByDescending(p => p.SoldCount)
            .Take(8)
            .ToListAsync();

        var featured = featuredRaw.Select(p =>
        {
            var firstSize = p.Sizes.OrderBy(s => s.DisplayOrder).FirstOrDefault();
            return new ProductCardVm
            {
                Id = p.Id,
                Name = p.Name,
                CategoryName = p.Category.Name,
                Description = p.Description,
                ImageUrl = p.ImageUrl,
                DisplayPrice = firstSize?.Price ?? p.BasePrice,
                DefaultSizeName = firstSize?.SizeName,
                IsAvailable = p.IsAvailable,
                IsFeatured = p.IsFeatured,
                SoldCount = p.SoldCount,
                LiquidColorHex = p.LiquidColorHex
            };
        }).ToList();

        var now = DateTime.UtcNow;
        var promos = await _db.Promotions
            .AsNoTracking()
            .Where(p => !p.IsDeleted && p.IsActive && p.StartAt <= now && p.EndAt >= now && (p.UsageLimit == 0 || p.UsedCount < p.UsageLimit))
            .OrderBy(p => p.EndAt)
            .Take(4)
            .ToListAsync();

        var tables3D = await _tableService.GetTablesFor3DAsync();
        foreach (var t in tables3D)
        {
            t.ActiveOrderCode = null;
            t.ActiveOrderTotal = null;
        }

        return View(new HomeIndexVm
        {
            Store = store,
            Categories = categories,
            FeaturedProducts = featured,
            ActivePromotions = promos,
            Tables3D = tables3D
        });
    }

    [HttpGet("/about")]
    public async Task<IActionResult> About()
    {
        var store = await _settingService.GetStoreSettingsAsync();
        ViewBag.Tables3D = await _tableService.GetTablesFor3DAsync();
        return View(store);
    }

    [HttpGet("/contact")]
    public async Task<IActionResult> Contact()
    {
        var store = await _settingService.GetStoreSettingsAsync();
        return View(store);
    }

    [HttpGet("/reservation")]
    public async Task<IActionResult> Reservation()
    {
        var tables = await _tableService.GetTablesFor3DAsync();
        foreach (var t in tables) { t.ActiveOrderCode = null; t.ActiveOrderTotal = null; }
        ViewBag.Tables3D = tables;

        var defaultLocal = FormatHelper.VnNow().AddHours(2);
        defaultLocal = new DateTime(defaultLocal.Year, defaultLocal.Month, defaultLocal.Day, defaultLocal.Hour, 0, 0);

        var vm = new ReservationCreateVm
        {
            ReservedAtLocal = defaultLocal,
            PartySize = 2
        };

        if (User.Identity?.IsAuthenticated == true)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                vm.CustomerName = user.FullName;
                vm.Phone = user.Phone ?? "";
            }
        }

        return View(vm);
    }

    [HttpPost("/reservation")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reservation(ReservationCreateVm model)
    {
        var tables = await _tableService.GetTablesFor3DAsync();
        foreach (var t in tables) { t.ActiveOrderCode = null; t.ActiveOrderTotal = null; }
        ViewBag.Tables3D = tables;

        if (!ModelState.IsValid)
            return View(model);

        var table = await _db.CafeTables.AsNoTracking().FirstOrDefaultAsync(t => t.Id == model.TableId && !t.IsDeleted && t.IsActive);
        if (table == null)
        {
            ModelState.AddModelError(nameof(model.TableId), "Bàn bạn chọn không hợp lệ.");
            return View(model);
        }

        // Chuyển giờ địa phương VN sang UTC
        var reservedUtc = DateTime.SpecifyKind(model.ReservedAtLocal.AddHours(-7), DateTimeKind.Utc);
        if (reservedUtc < DateTime.UtcNow.AddMinutes(15))
        {
            ModelState.AddModelError(nameof(model.ReservedAtLocal), "Vui lòng đặt bàn trước ít nhất 15 phút so với hiện tại.");
            return View(model);
        }

        _db.TableReservations.Add(new TableReservation
        {
            TableId = table.Id,
            CustomerName = FormatHelper.SanitizeNote(model.CustomerName, 120),
            Phone = model.Phone.Trim(),
            PartySize = model.PartySize,
            ReservedAt = reservedUtc,
            Status = ReservationStatus.Pending,
            Note = FormatHelper.SanitizeNote(model.Note, 200),
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        TempData["ToastSuccess"] = $"Đã gửi yêu cầu đặt bàn {table.Code} lúc {model.ReservedAtLocal:dd/MM/yyyy HH:mm}. Nhân viên sẽ xác nhận sớm nhất!";
        return RedirectToAction(nameof(Reservation));
    }
}
