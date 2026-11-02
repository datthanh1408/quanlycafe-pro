using System.Security.Claims;
using CoffeeShop.Areas.Admin.Models;
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

namespace CoffeeShop.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = AppRoles.Admin)]
public class UsersAdminController : Controller
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditService _auditService;

    public UsersAdminController(
        AppDbContext db,
        UserManager<ApplicationUser> userManager,
        IAuditService auditService)
    {
        _db = db;
        _userManager = userManager;
        _auditService = auditService;
    }

    // =========================================================================
    // 1. EMPLOYEES & PERMISSIONS (/admin/employees)
    // =========================================================================
    [HttpGet("/admin/employees")]
    public async Task<IActionResult> Employees()
    {
        var currentUserId = _userManager.GetUserId(User) ?? string.Empty;

        var adminUsers = await _userManager.GetUsersInRoleAsync(AppRoles.Admin);
        var staffUsers = await _userManager.GetUsersInRoleAsync(AppRoles.Staff);

        var adminIdSet = adminUsers.Select(u => u.Id).ToHashSet();
        var combinedIds = adminUsers.Concat(staffUsers)
            .Where(u => !u.IsDeleted)
            .Select(u => u.Id)
            .Distinct()
            .ToList();

        var profiles = await _db.EmployeeProfiles
            .AsNoTracking()
            .Where(ep => combinedIds.Contains(ep.UserId))
            .ToDictionaryAsync(ep => ep.UserId);

        var claimsLookup = await _db.UserClaims
            .AsNoTracking()
            .Where(uc => combinedIds.Contains(uc.UserId) && uc.ClaimType == StaffPermissions.ClaimType)
            .ToListAsync();

        var users = await _db.Users
            .AsNoTracking()
            .Where(u => combinedIds.Contains(u.Id) && !u.IsDeleted)
            .OrderByDescending(u => adminIdSet.Contains(u.Id))
            .ThenBy(u => u.FullName)
            .ToListAsync();

        var rows = new List<EmployeeRowVm>();
        foreach (var u in users)
        {
            var isAdm = adminIdSet.Contains(u.Id);
            profiles.TryGetValue(u.Id, out var prof);
            var perms = claimsLookup
                .Where(c => c.UserId == u.Id && !string.IsNullOrWhiteSpace(c.ClaimValue))
                .Select(c => c.ClaimValue!)
                .Distinct()
                .ToList();

            rows.Add(new EmployeeRowVm
            {
                Id = u.Id,
                FullName = u.FullName,
                Email = u.Email ?? u.UserName ?? "",
                Phone = u.Phone ?? u.PhoneNumber,
                Role = isAdm ? AppRoles.Admin : AppRoles.Staff,
                IsActive = u.IsActive,
                MustChangePassword = u.MustChangePassword,
                Position = prof?.Position ?? (isAdm ? "Quản trị viên" : "Barista / Thu ngân"),
                HireDate = prof?.HireDate ?? u.CreatedAt,
                WorkStatus = prof?.WorkStatus ?? "Đang làm",
                GrantedPermissions = perms
            });
        }

        var activeAdminCount = rows.Count(r => r.Role == AppRoles.Admin && r.IsActive);

        return View("Employees", new EmployeesAdminViewModel
        {
            Employees = rows,
            CurrentUserId = currentUserId,
            ActiveAdminCount = activeAdminCount
        });
    }

    [HttpPost("/admin/employees/create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateEmployee(CreateEmployeeFormModel form)
    {
        if (string.IsNullOrWhiteSpace(form.FullName) ||
            string.IsNullOrWhiteSpace(form.Email) ||
            string.IsNullOrWhiteSpace(form.TemporaryPassword))
        {
            TempData["ToastError"] = "Vui lòng nhập đầy đủ Họ tên, Email và Mật khẩu tạm thời.";
            return RedirectToAction(nameof(Employees));
        }

        var cleanEmail = form.Email.Trim().ToLowerInvariant();
        var existing = await _userManager.FindByEmailAsync(cleanEmail);
        if (existing != null)
        {
            TempData["ToastError"] = $"Email '{cleanEmail}' đã được đăng ký trong hệ thống.";
            return RedirectToAction(nameof(Employees));
        }

        var user = new ApplicationUser
        {
            UserName = cleanEmail,
            Email = cleanEmail,
            EmailConfirmed = true,
            FullName = form.FullName.Trim(),
            Phone = string.IsNullOrWhiteSpace(form.Phone) ? null : form.Phone.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(form.Phone) ? null : form.Phone.Trim(),
            IsActive = true,
            IsDeleted = false,
            MustChangePassword = true, // Bắt buộc đổi mật khẩu ở lần đăng nhập đầu tiên (Mục 7.3)
            CreatedAt = DateTime.UtcNow
        };

        var createRes = await _userManager.CreateAsync(user, form.TemporaryPassword);
        if (!createRes.Succeeded)
        {
            var errMsg = string.Join(" ", createRes.Errors.Select(e => e.Description));
            TempData["ToastError"] = $"Không thể tạo tài khoản nhân viên: {errMsg} (Yêu cầu >= 8 ký tự, gồm chữ hoa, chữ thường và chữ số).";
            return RedirectToAction(nameof(Employees));
        }

        await _userManager.AddToRoleAsync(user, AppRoles.Staff);

        var profile = new EmployeeProfile
        {
            UserId = user.Id,
            Position = string.IsNullOrWhiteSpace(form.Position) ? "Barista / Thu ngân" : form.Position.Trim(),
            HireDate = form.HireDate == default ? DateTime.UtcNow : DateTime.SpecifyKind(form.HireDate.Date, DateTimeKind.Utc),
            WorkStatus = string.IsNullOrWhiteSpace(form.WorkStatus) ? "Đang làm" : form.WorkStatus.Trim()
        };
        _db.EmployeeProfiles.Add(profile);
        await _db.SaveChangesAsync();

        var validPerms = (form.Permissions ?? new List<string>())
            .Where(p => StaffPermissions.All.ContainsKey(p))
            .Distinct()
            .ToList();

        if (validPerms.Count > 0)
        {
            var claims = validPerms.Select(p => new Claim(StaffPermissions.ClaimType, p));
            await _userManager.AddClaimsAsync(user, claims);
        }

        await _auditService.LogAsync("CreateEmployee", "ApplicationUser", user.Id, null, new
        {
            user.Email,
            user.FullName,
            profile.Position,
            MustChangePassword = true,
            Permissions = validPerms
        });

        TempData["ToastSuccess"] = $"Đã tạo nhân viên '{user.FullName}' ({user.Email}). Nhân viên sẽ được yêu cầu đổi mật khẩu ngay khi đăng nhập.";
        return RedirectToAction(nameof(Employees));
    }

    [HttpPost("/admin/employees/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditEmployee(EditEmployeeFormModel form)
    {
        if (string.IsNullOrWhiteSpace(form.UserId) || string.IsNullOrWhiteSpace(form.FullName))
        {
            TempData["ToastError"] = "Thông tin nhân viên không hợp lệ.";
            return RedirectToAction(nameof(Employees));
        }

        var user = await _userManager.FindByIdAsync(form.UserId);
        if (user == null || user.IsDeleted)
        {
            TempData["ToastError"] = "Không tìm thấy tài khoản nhân viên.";
            return RedirectToAction(nameof(Employees));
        }

        user.FullName = form.FullName.Trim();
        user.Phone = string.IsNullOrWhiteSpace(form.Phone) ? null : form.Phone.Trim();
        user.PhoneNumber = user.Phone;
        await _userManager.UpdateAsync(user);

        var profile = await _db.EmployeeProfiles.FirstOrDefaultAsync(ep => ep.UserId == user.Id);
        if (profile == null)
        {
            profile = new EmployeeProfile { UserId = user.Id };
            _db.EmployeeProfiles.Add(profile);
        }

        profile.Position = string.IsNullOrWhiteSpace(form.Position) ? "Barista / Thu ngân" : form.Position.Trim();
        profile.HireDate = form.HireDate == default ? DateTime.UtcNow : DateTime.SpecifyKind(form.HireDate.Date, DateTimeKind.Utc);
        profile.WorkStatus = string.IsNullOrWhiteSpace(form.WorkStatus) ? "Đang làm" : form.WorkStatus.Trim();
        await _db.SaveChangesAsync();

        // Cập nhật các quyền mở rộng ◐ (StaffPermissions) nếu tài khoản là Staff
        var isAdminAccount = await _userManager.IsInRoleAsync(user, AppRoles.Admin);
        var existingClaims = await _userManager.GetClaimsAsync(user);
        var oldPermClaims = existingClaims.Where(c => c.Type == StaffPermissions.ClaimType).ToList();
        var oldPermList = oldPermClaims.Select(c => c.Value).OrderBy(x => x).ToList();

        var newPermList = isAdminAccount
            ? new List<string>()
            : (form.Permissions ?? new List<string>())
                .Where(p => StaffPermissions.All.ContainsKey(p))
                .Distinct()
                .OrderBy(x => x)
                .ToList();

        if (!isAdminAccount)
        {
            if (oldPermClaims.Count > 0)
            {
                await _userManager.RemoveClaimsAsync(user, oldPermClaims);
            }
            if (newPermList.Count > 0)
            {
                await _userManager.AddClaimsAsync(user, newPermList.Select(p => new Claim(StaffPermissions.ClaimType, p)));
            }
            await _userManager.UpdateSecurityStampAsync(user);
        }

        await _auditService.LogAsync(
            action: "UpdateEmployeeAndPermissions",
            entityName: "ApplicationUser",
            entityId: user.Id,
            oldValues: new { Permissions = oldPermList },
            newValues: new { user.FullName, profile.Position, profile.WorkStatus, Permissions = newPermList });

        TempData["ToastSuccess"] = $"Đã cập nhật hồ sơ & phân quyền cho '{user.FullName}'.";
        return RedirectToAction(nameof(Employees));
    }

    [HttpPost("/admin/employees/toggle-lock")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleEmployeeLock(string userId)
    {
        var currentAdminId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
        {
            TempData["ToastError"] = "Không xác định được tài khoản.";
            return RedirectToAction(nameof(Employees));
        }

        // Quy tắc Mục 5.1: CHẶN tự khóa chính mình
        if (userId == currentAdminId)
        {
            TempData["ToastError"] = "Bạn không thể tự khóa tài khoản Quản trị viên đang đăng nhập của chính mình!";
            return RedirectToAction(nameof(Employees));
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null || user.IsDeleted)
        {
            TempData["ToastError"] = "Không tìm thấy tài khoản.";
            return RedirectToAction(nameof(Employees));
        }

        // Quy tắc Mục 5.1: CHẶN khóa Admin hoạt động cuối cùng
        if (user.IsActive && await _userManager.IsInRoleAsync(user, AppRoles.Admin))
        {
            var allAdmins = await _userManager.GetUsersInRoleAsync(AppRoles.Admin);
            var activeAdminsCount = allAdmins.Count(a => a.IsActive && !a.IsDeleted);
            if (activeAdminsCount <= 1)
            {
                TempData["ToastError"] = "Không thể khóa tài khoản Quản trị viên hoạt động cuối cùng của hệ thống!";
                return RedirectToAction(nameof(Employees));
            }
        }

        user.IsActive = !user.IsActive;
        user.LockoutEnd = user.IsActive ? null : DateTimeOffset.MaxValue;
        await _userManager.UpdateAsync(user);
        await _userManager.UpdateSecurityStampAsync(user);

        await _auditService.LogAsync(
            action: user.IsActive ? "UnlockEmployee" : "LockEmployee",
            entityName: "ApplicationUser",
            entityId: user.Id,
            oldValues: new { IsActive = !user.IsActive },
            newValues: new { user.Email, user.IsActive });

        TempData["ToastSuccess"] = $"Đã {(user.IsActive ? "mở khóa" : "khóa")} tài khoản '{user.FullName}'.";
        return RedirectToAction(nameof(Employees));
    }

    [HttpPost("/admin/employees/reset-password")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetEmployeePassword(string userId, string temporaryPassword)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(temporaryPassword))
        {
            TempData["ToastError"] = "Vui lòng nhập mật khẩu tạm thời mới.";
            return RedirectToAction(nameof(Employees));
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null || user.IsDeleted)
        {
            TempData["ToastError"] = "Không tìm thấy tài khoản nhân viên.";
            return RedirectToAction(nameof(Employees));
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var res = await _userManager.ResetPasswordAsync(user, token, temporaryPassword);
        if (!res.Succeeded)
        {
            var errMsg = string.Join(" ", res.Errors.Select(e => e.Description));
            TempData["ToastError"] = $"Mật khẩu tạm không đạt yêu cầu bảo mật: {errMsg}";
            return RedirectToAction(nameof(Employees));
        }

        user.MustChangePassword = true;
        await _userManager.UpdateAsync(user);
        await _userManager.UpdateSecurityStampAsync(user);

        await _auditService.LogAsync("ResetEmployeeTempPassword", "ApplicationUser", user.Id, null,
            new { user.Email, MustChangePassword = true });

        TempData["ToastSuccess"] = $"Đã cấp lại mật khẩu tạm thời cho '{user.FullName}'. Nhân viên sẽ phải đổi mật khẩu khi đăng nhập.";
        return RedirectToAction(nameof(Employees));
    }

    // =========================================================================
    // 2. CUSTOMERS (/admin/customers)
    // =========================================================================
    [HttpGet("/admin/customers")]
    public async Task<IActionResult> Customers(string? search = null, string sort = "newest", int page = 1)
    {
        const int pageSize = 15;
        page = Math.Max(1, page);
        var normalizedSort = (sort ?? "newest").Trim().ToLowerInvariant();

        var customerRoleId = await _db.Roles
            .AsNoTracking()
            .Where(r => r.Name == AppRoles.Customer)
            .Select(r => r.Id)
            .FirstOrDefaultAsync();

        var customerUserIds = _db.UserRoles
            .Where(ur => ur.RoleId == customerRoleId)
            .Select(ur => ur.UserId);

        var baseQuery = _db.Users
            .AsNoTracking()
            .Where(u => !u.IsDeleted && customerUserIds.Contains(u.Id));

        if (!string.IsNullOrWhiteSpace(search))
        {
            var kw = search.Trim();
            baseQuery = baseQuery.Where(u =>
                u.FullName.Contains(kw) ||
                (u.Email != null && u.Email.Contains(kw)) ||
                (u.Phone != null && u.Phone.Contains(kw)));
        }

        // Quy tắc Mục 7.3: Chi tiêu tích lũy của khách CHỈ tính đơn Completed && Paid
        var projected = baseQuery.Select(u => new CustomerRowVm
        {
            Id = u.Id,
            FullName = u.FullName,
            Email = u.Email ?? "",
            Phone = u.Phone ?? u.PhoneNumber,
            Address = u.Address,
            IsActive = u.IsActive,
            LoyaltyPoints = u.LoyaltyPoints,
            CreatedAt = u.CreatedAt,
            CompletedOrderCount = u.CustomerOrders.Count(o => o.Status == OrderStatus.Completed && o.PaymentStatus == PaymentStatus.Paid),
            TotalSpend = u.CustomerOrders
                .Where(o => o.Status == OrderStatus.Completed && o.PaymentStatus == PaymentStatus.Paid)
                .Sum(o => (decimal?)o.Total) ?? 0m
        });

        projected = normalizedSort switch
        {
            "spend_desc" => projected.OrderByDescending(x => x.TotalSpend).ThenByDescending(x => x.CompletedOrderCount),
            "orders_desc" => projected.OrderByDescending(x => x.CompletedOrderCount).ThenByDescending(x => x.TotalSpend),
            _ => projected.OrderByDescending(x => x.CreatedAt)
        };

        var totalItems = await projected.CountAsync();
        var items = await projected
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var qs = new List<string>();
        if (!string.IsNullOrWhiteSpace(search)) qs.Add($"search={Uri.EscapeDataString(search.Trim())}");
        if (normalizedSort != "newest") qs.Add($"sort={Uri.EscapeDataString(normalizedSort)}");

        var baseQueryUrl = qs.Count > 0 ? $"/admin/customers?{string.Join("&", qs)}" : "/admin/customers";

        return View("Customers", new CustomersAdminViewModel
        {
            Search = search,
            Sort = normalizedSort,
            BaseQueryUrl = baseQueryUrl,
            PagedCustomers = new PagedResult<CustomerRowVm>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalItems = totalItems
            }
        });
    }

    [HttpGet("/admin/customers/detail/{id}")]
    [HttpGet("/admin/customers/{id}")]
    public async Task<IActionResult> CustomerDetail(string id)
    {
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted);

        if (user == null)
        {
            TempData["ToastError"] = "Không tìm thấy hồ sơ khách hàng.";
            return RedirectToAction(nameof(Customers));
        }

        var orders = await _db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .Include(o => o.Table)
            .Where(o => o.CustomerId == user.Id)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        var completedPaid = orders
            .Where(o => o.Status == OrderStatus.Completed && o.PaymentStatus == PaymentStatus.Paid)
            .ToList();

        var vm = new CustomerDetailViewModel
        {
            Customer = new CustomerRowVm
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email ?? "",
                Phone = user.Phone ?? user.PhoneNumber,
                Address = user.Address,
                IsActive = user.IsActive,
                LoyaltyPoints = user.LoyaltyPoints,
                CreatedAt = user.CreatedAt,
                CompletedOrderCount = completedPaid.Count,
                TotalSpend = completedPaid.Sum(o => o.Total)
            },
            Orders = orders
        };

        return View("CustomerDetail", vm);
    }

    [HttpPost("/admin/customers/toggle-lock")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleCustomerLock(string userId, string? returnUrl = null)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null || user.IsDeleted)
        {
            TempData["ToastError"] = "Không tìm thấy tài khoản khách hàng.";
            return RedirectToAction(nameof(Customers));
        }

        if (await _userManager.IsInRoleAsync(user, AppRoles.Admin))
        {
            TempData["ToastError"] = "Vui lòng quản lý tài khoản Quản trị tại mục Nhân viên & Quyền.";
            return RedirectToAction(nameof(Customers));
        }

        user.IsActive = !user.IsActive;
        user.LockoutEnd = user.IsActive ? null : DateTimeOffset.MaxValue;
        await _userManager.UpdateAsync(user);
        await _userManager.UpdateSecurityStampAsync(user);

        await _auditService.LogAsync(
            action: user.IsActive ? "UnlockCustomer" : "LockCustomer",
            entityName: "ApplicationUser",
            entityId: user.Id,
            oldValues: new { IsActive = !user.IsActive },
            newValues: new { user.Email, user.IsActive });

        TempData["ToastSuccess"] = $"Đã {(user.IsActive ? "mở khóa" : "khóa")} tài khoản khách hàng '{user.FullName}'.";

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction(nameof(Customers));
    }
}
