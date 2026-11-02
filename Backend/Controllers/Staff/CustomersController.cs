using CoffeeShop.Areas.Staff.Models;
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

namespace CoffeeShop.Areas.Staff.Controllers;

[Area("Staff")]
[Authorize(Policy = AppPolicies.StaffOrAdmin)]
public class CustomersController : Controller
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditService _auditService;
    private readonly IShiftService _shiftService;

    public CustomersController(
        AppDbContext db,
        UserManager<ApplicationUser> userManager,
        IAuditService auditService,
        IShiftService shiftService)
    {
        _db = db;
        _userManager = userManager;
        _auditService = auditService;
        _shiftService = shiftService;
    }

    [HttpGet("/staff/customers")]
    public async Task<IActionResult> Index(string? q = null, int page = 1)
    {
        const int pageSize = 12;
        page = Math.Max(1, page);

        var customerRoleId = await _db.Roles
            .AsNoTracking()
            .Where(r => r.Name == AppRoles.Customer)
            .Select(r => r.Id)
            .FirstOrDefaultAsync();

        var query = _db.Users
            .AsNoTracking()
            .Where(u => !u.IsDeleted);

        if (!string.IsNullOrWhiteSpace(customerRoleId))
        {
            query = query.Where(u => _db.UserRoles.Any(ur => ur.UserId == u.Id && ur.RoleId == customerRoleId));
        }

        var cleanQ = q?.Trim();
        if (!string.IsNullOrWhiteSpace(cleanQ))
        {
            query = query.Where(u =>
                u.FullName.Contains(cleanQ) ||
                (u.Phone != null && u.Phone.Contains(cleanQ)) ||
                (u.PhoneNumber != null && u.PhoneNumber.Contains(cleanQ)) ||
                (u.Email != null && u.Email.Contains(cleanQ)));
        }

        var totalItems = await query.CountAsync();

        var usersPage = await query
            .OrderByDescending(u => u.LoyaltyPoints)
            .ThenByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var userIds = usersPage.Select(u => u.Id).ToList();

        var ordersForUsers = await _db.Orders
            .AsNoTracking()
            .Where(o => o.CustomerId != null && userIds.Contains(o.CustomerId))
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => new
            {
                o.Id,
                o.CustomerId,
                o.Code,
                o.CreatedAt,
                o.Total,
                o.Status,
                o.PaymentStatus
            })
            .ToListAsync();

        var items = usersPage.Select(u =>
        {
            var custOrders = ordersForUsers.Where(o => o.CustomerId == u.Id).ToList();
            return new StaffCustomerItemVm
            {
                Id = u.Id,
                FullName = u.FullName,
                Email = u.Email ?? string.Empty,
                Phone = u.Phone ?? u.PhoneNumber,
                Address = u.Address,
                LoyaltyPoints = u.LoyaltyPoints,
                IsActive = u.IsActive,
                CreatedAt = u.CreatedAt,
                OrderCount = custOrders.Count,
                TotalSpent = custOrders
                    .Where(o => o.Status == OrderStatus.Completed && o.PaymentStatus == PaymentStatus.Paid)
                    .Sum(o => o.Total),
                RecentOrders = custOrders
                    .Take(5)
                    .Select(o => new StaffCustomerRecentOrderVm
                    {
                        Id = o.Id,
                        Code = o.Code,
                        CreatedAt = o.CreatedAt,
                        Total = o.Total,
                        Status = o.Status,
                        PaymentStatus = o.PaymentStatus
                    })
                    .ToList()
            };
        }).ToList();

        var baseQueryUrl = !string.IsNullOrWhiteSpace(cleanQ)
            ? $"/staff/customers?q={Uri.EscapeDataString(cleanQ)}"
            : "/staff/customers";

        var vm = new StaffCustomerListVm
        {
            Customers = new PagedResult<StaffCustomerItemVm>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalItems = totalItems
            },
            SearchKeyword = cleanQ,
            BaseQueryUrl = baseQueryUrl,
            CanEditCustomer = AppPolicies.HasAdminOrClaim(User, StaffPermissions.EditCustomer)
        };

        return View(vm);
    }

    [HttpPost("/staff/customers/edit")]
    [Authorize(Policy = AppPolicies.CanEditCustomer)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(StaffEditCustomerInputDto input)
    {
        var redirectUrl = !string.IsNullOrWhiteSpace(input.ReturnQuery)
            ? $"/staff/customers?q={Uri.EscapeDataString(input.ReturnQuery)}"
            : "/staff/customers";

        if (!ModelState.IsValid)
        {
            var firstErr = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .FirstOrDefault();
            TempData["ToastError"] = firstErr ?? "Dữ liệu cập nhật khách hàng không hợp lệ.";
            return Redirect(redirectUrl);
        }

        var customer = await _userManager.FindByIdAsync(input.Id);
        if (customer == null || customer.IsDeleted)
        {
            TempData["ToastError"] = "Không tìm thấy khách hàng.";
            return Redirect(redirectUrl);
        }

        // Chỉ cho phép chỉnh sửa tài khoản vai trò Customer (không chỉnh sửa tài khoản Admin/Staff qua màn này)
        var roles = await _userManager.GetRolesAsync(customer);
        if (!roles.Contains(AppRoles.Customer) || roles.Contains(AppRoles.Admin) || roles.Contains(AppRoles.Staff))
        {
            TempData["ToastError"] = "Chỉ được phép cập nhật thông tin tài khoản Khách hàng.";
            return Redirect(redirectUrl);
        }

        var oldVals = new
        {
            customer.FullName,
            Phone = customer.Phone ?? customer.PhoneNumber,
            customer.Address
        };

        customer.FullName = FormatHelper.SanitizeNote(input.FullName, 120);
        customer.Phone = input.Phone.Trim();
        customer.PhoneNumber = input.Phone.Trim();
        customer.Address = FormatHelper.SanitizeNote(input.Address, 300);

        var res = await _userManager.UpdateAsync(customer);
        if (!res.Succeeded)
        {
            TempData["ToastError"] = "Không thể lưu cập nhật thông tin khách hàng.";
            return Redirect(redirectUrl);
        }

        await _auditService.LogAsync(
            action: "EditCustomer",
            entityName: "ApplicationUser",
            entityId: customer.Email,
            oldValues: oldVals,
            newValues: new { customer.FullName, customer.Phone, customer.Address });

        TempData["ToastSuccess"] = $"Đã cập nhật thông tin khách hàng '{customer.FullName}'.";
        return Redirect(redirectUrl);
    }

    [HttpGet("/staff/availability")]
    [Authorize(Policy = AppPolicies.CanToggleProduct)]
    public async Task<IActionResult> Availability(int? categoryId = null, string? q = null, string filter = "all")
    {
        var categories = await _db.Categories
            .AsNoTracking()
            .Where(c => !c.IsDeleted && c.IsActive)
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Name)
            .ToListAsync();

        var baseProductsQuery = _db.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Where(p => !p.IsDeleted && !p.Category.IsDeleted && p.Category.IsActive);

        var availableCount = await baseProductsQuery.CountAsync(p => p.IsAvailable);
        var unavailableCount = await baseProductsQuery.CountAsync(p => !p.IsAvailable);

        if (categoryId.HasValue)
        {
            baseProductsQuery = baseProductsQuery.Where(p => p.CategoryId == categoryId.Value);
        }

        var cleanQ = q?.Trim();
        if (!string.IsNullOrWhiteSpace(cleanQ))
        {
            baseProductsQuery = baseProductsQuery.Where(p => p.Name.Contains(cleanQ));
        }

        var normalizedFilter = (filter ?? "all").ToLowerInvariant();
        if (normalizedFilter == "available")
        {
            baseProductsQuery = baseProductsQuery.Where(p => p.IsAvailable);
        }
        else if (normalizedFilter == "unavailable")
        {
            baseProductsQuery = baseProductsQuery.Where(p => !p.IsAvailable);
        }
        else
        {
            normalizedFilter = "all";
        }

        var products = await baseProductsQuery
            .OrderBy(p => p.IsAvailable) // Ưu tiên hiện món đang Tạm hết lên trước hoặc gom rõ ràng
            .ThenBy(p => p.Category.DisplayOrder)
            .ThenBy(p => p.Name)
            .Select(p => new StaffAvailabilityItemVm
            {
                Id = p.Id,
                Name = p.Name,
                CategoryId = p.CategoryId,
                CategoryName = p.Category.Name,
                ImageUrl = p.ImageUrl,
                BasePrice = p.BasePrice,
                IsAvailable = p.IsAvailable,
                SoldCount = p.SoldCount
            })
            .ToListAsync();

        var vm = new StaffAvailabilityVm
        {
            Categories = categories,
            Products = products,
            CategoryId = categoryId,
            SearchKeyword = cleanQ,
            FilterState = normalizedFilter,
            AvailableCount = availableCount,
            UnavailableCount = unavailableCount
        };

        return View("Availability", vm);
    }

    [HttpPost("/staff/availability/toggle")]
    [Authorize(Policy = AppPolicies.CanToggleProduct)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleAvailability(
        int productId,
        int? categoryId = null,
        string? q = null,
        string filter = "all")
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == productId && !p.IsDeleted);
        if (product == null)
        {
            TempData["ToastError"] = "Không tìm thấy sản phẩm.";
            return Redirect("/staff/availability");
        }

        var prevState = product.IsAvailable;
        product.IsAvailable = !product.IsAvailable;
        await _db.SaveChangesAsync();

        await _auditService.LogAsync(
            action: "ToggleProductAvailability",
            entityName: "Product",
            entityId: product.Id.ToString(),
            oldValues: new { product.Name, IsAvailable = prevState },
            newValues: new { product.Name, product.IsAvailable });

        TempData["ToastSuccess"] = product.IsAvailable
            ? $"Đã mở bán lại món '{product.Name}' (Còn hàng)."
            : $"Đã đánh dấu món '{product.Name}' là TẠM HẾT.";

        var queryParts = new List<string>();
        if (categoryId.HasValue) queryParts.Add($"categoryId={categoryId.Value}");
        if (!string.IsNullOrWhiteSpace(q)) queryParts.Add($"q={Uri.EscapeDataString(q.Trim())}");
        if (!string.IsNullOrWhiteSpace(filter) && filter != "all") queryParts.Add($"filter={Uri.EscapeDataString(filter)}");

        var redirectUrl = queryParts.Count > 0
            ? $"/staff/availability?{string.Join("&", queryParts)}"
            : "/staff/availability";

        return Redirect(redirectUrl);
    }

    [HttpGet("/staff/profile")]
    public async Task<IActionResult> Profile()
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null)
            return Redirect("/login");

        var profile = await _db.EmployeeProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(ep => ep.UserId == currentUser.Id);

        var roles = (await _userManager.GetRolesAsync(currentUser)).ToList();
        var activeShift = await _shiftService.GetActiveShiftAsync(currentUser.Id);

        var recentShifts = await _db.Shifts
            .AsNoTracking()
            .Where(s => s.StaffId == currentUser.Id)
            .OrderByDescending(s => s.OpenedAt)
            .Take(10)
            .ToListAsync();

        var permissions = StaffPermissions.All.Select(kv => new StaffPermissionStatusVm
        {
            ClaimValue = kv.Key,
            Description = kv.Value,
            IsGranted = AppPolicies.HasAdminOrClaim(User, kv.Key)
        }).ToList();

        var vm = new StaffProfileVm
        {
            FullName = currentUser.FullName,
            Email = currentUser.Email ?? string.Empty,
            Phone = currentUser.Phone ?? currentUser.PhoneNumber,
            Address = currentUser.Address,
            AvatarUrl = currentUser.AvatarUrl,
            Roles = roles,
            Position = profile?.Position ?? (roles.Contains(AppRoles.Admin) ? "Quản trị viên hệ thống" : "Barista / Thu ngân"),
            WorkStatus = profile?.WorkStatus ?? "Đang làm",
            HireDate = profile?.HireDate ?? currentUser.CreatedAt,
            Permissions = permissions,
            ActiveShift = activeShift,
            RecentShifts = recentShifts
        };

        return View("Profile", vm);
    }
}
