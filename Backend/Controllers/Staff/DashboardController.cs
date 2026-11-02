using CoffeeShop.Areas.Staff.Models;
using CoffeeShop.Authorization;
using CoffeeShop.Data;
using CoffeeShop.Helpers;
using CoffeeShop.Models.Entities;
using CoffeeShop.Models.Enums;
using CoffeeShop.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShop.Areas.Staff.Controllers;

[Area("Staff")]
[Authorize(Policy = AppPolicies.StaffOrAdmin)]
public class DashboardController : Controller
{
    private readonly AppDbContext _db;
    private readonly IShiftService _shiftService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditService _auditService;

    public DashboardController(
        AppDbContext db,
        IShiftService shiftService,
        UserManager<ApplicationUser> userManager,
        IAuditService auditService)
    {
        _db = db;
        _shiftService = shiftService;
        _userManager = userManager;
        _auditService = auditService;
    }

    [HttpGet("/staff")]
    [HttpGet("/staff/dashboard")]
    public async Task<IActionResult> Index()
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null)
            return Redirect("/login");

        var staffId = currentUser.Id;
        var activeShift = await _shiftService.GetActiveShiftAsync(staffId);
        var todayStartUtc = FormatHelper.VnDateToUtcStart(FormatHelper.VnNow());

        Shift? lastClosedToday = null;
        decimal liveCashCollected = 0m;
        decimal liveExpectedCash = 0m;

        if (activeShift != null)
        {
            liveCashCollected = await _db.Orders
                .AsNoTracking()
                .Where(o => (o.ShiftId == activeShift.Id || (o.PaidAt >= activeShift.OpenedAt && o.StaffId == staffId)) &&
                            o.PaymentMethod == "CASH" &&
                            o.PaymentStatus == PaymentStatus.Paid)
                .SumAsync(o => (decimal?)o.Total) ?? 0m;

            liveExpectedCash = activeShift.OpeningCash + liveCashCollected;
        }
        else
        {
            lastClosedToday = await _db.Shifts
                .AsNoTracking()
                .Where(s => s.StaffId == staffId && s.ClosedAt != null && s.ClosedAt >= todayStartUtc)
                .OrderByDescending(s => s.ClosedAt)
                .FirstOrDefaultAsync();
        }

        // Đếm các đơn đang mở trên toàn quán để cảnh báo khi đóng ca và theo dõi vận hành
        var openOrdersCount = await _db.Orders
            .AsNoTracking()
            .CountAsync(o => o.Status != OrderStatus.Completed && o.Status != OrderStatus.Cancelled);

        var pendingCount = await _db.Orders
            .AsNoTracking()
            .CountAsync(o => o.Status == OrderStatus.Pending);

        var processingCount = await _db.Orders
            .AsNoTracking()
            .CountAsync(o => o.Status == OrderStatus.Confirmed ||
                             o.Status == OrderStatus.Brewing ||
                             o.Status == OrderStatus.Ready ||
                             o.Status == OrderStatus.Delivering);

        int totalInPeriod;
        int completedInPeriod;

        if (activeShift != null)
        {
            totalInPeriod = await _db.Orders
                .AsNoTracking()
                .CountAsync(o => o.ShiftId == activeShift.Id || o.CreatedAt >= activeShift.OpenedAt);

            completedInPeriod = await _db.Orders
                .AsNoTracking()
                .CountAsync(o => (o.ShiftId == activeShift.Id || o.UpdatedAt >= activeShift.OpenedAt) &&
                                 o.Status == OrderStatus.Completed);
        }
        else
        {
            totalInPeriod = await _db.Orders
                .AsNoTracking()
                .CountAsync(o => o.CreatedAt >= todayStartUtc);

            completedInPeriod = await _db.Orders
                .AsNoTracking()
                .CountAsync(o => o.CreatedAt >= todayStartUtc && o.Status == OrderStatus.Completed);
        }

        // Doanh thu ca CHỈ tính và hiển thị khi được cấp quyền Staff.ViewRevenue (hoặc Admin)
        // Và CHỈ tính đơn Completed && Paid
        bool canViewRevenue = AppPolicies.HasAdminOrClaim(User, StaffPermissions.ViewRevenue);
        decimal? shiftRevenue = null;

        if (canViewRevenue)
        {
            if (activeShift != null)
            {
                shiftRevenue = await _db.Orders
                    .AsNoTracking()
                    .Where(o => (o.ShiftId == activeShift.Id || o.UpdatedAt >= activeShift.OpenedAt) &&
                                o.Status == OrderStatus.Completed &&
                                o.PaymentStatus == PaymentStatus.Paid)
                    .SumAsync(o => (decimal?)o.Total) ?? 0m;
            }
            else
            {
                shiftRevenue = await _db.Orders
                    .AsNoTracking()
                    .Where(o => o.CreatedAt >= todayStartUtc &&
                                o.Status == OrderStatus.Completed &&
                                o.PaymentStatus == PaymentStatus.Paid)
                    .SumAsync(o => (decimal?)o.Total) ?? 0m;
            }
        }

        // 10 đơn hàng mới nhất
        var latestRaw = await _db.Orders
            .AsNoTracking()
            .Include(o => o.Table)
            .Include(o => o.Items)
            .OrderByDescending(o => o.CreatedAt)
            .Take(10)
            .ToListAsync();

        var latestOrders = latestRaw.Select(o => new StaffOrderListItemVm
        {
            Id = o.Id,
            Code = o.Code,
            CustomerName = o.CustomerName,
            Phone = o.Phone,
            OrderType = o.OrderType,
            Source = o.Source,
            Status = o.Status,
            PaymentStatus = o.PaymentStatus,
            PaymentMethod = o.PaymentMethod,
            TableCode = o.Table?.Code,
            ItemCount = o.Items.Sum(i => i.Quantity),
            Total = o.Total,
            CreatedAt = o.CreatedAt,
            NextForwardStatus = OrdersController.GetNextForwardStatus(o.OrderType, o.Status),
            RowVersionBase64 = o.RowVersion.Length > 0 ? Convert.ToBase64String(o.RowVersion) : null
        }).ToList();

        var vm = new StaffDashboardVm
        {
            StaffName = currentUser.FullName,
            ActiveShift = activeShift,
            LastClosedShiftToday = lastClosedToday,
            LiveCashCollected = liveCashCollected,
            LiveExpectedCash = liveExpectedCash,
            OpenOrdersCount = openOrdersCount,
            PeriodLabel = activeShift != null
                ? $"Trong ca #{activeShift.Id} (từ {FormatHelper.DateTimeVn(activeShift.OpenedAt, "HH:mm")})"
                : $"Hôm nay ({FormatHelper.VnNow():dd/MM/yyyy})",
            TotalOrdersInShift = totalInPeriod,
            PendingOrdersCount = pendingCount,
            ProcessingOrdersCount = processingCount,
            CompletedOrdersCount = completedInPeriod,
            CanViewRevenue = canViewRevenue,
            ShiftRevenue = shiftRevenue,
            LatestOrders = latestOrders
        };

        return View(vm);
    }

    [HttpPost("/staff/dashboard/open-shift")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> OpenShift(OpenShiftInputDto input)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null)
            return Redirect("/login");

        if (!ModelState.IsValid)
        {
            TempData["ToastError"] = "Thông tin mở ca không hợp lệ. Vui lòng kiểm tra lại số tiền đầu ca.";
            return Redirect("/staff/dashboard");
        }

        var (ok, shift, err) = await _shiftService.OpenShiftAsync(currentUser.Id, input.OpeningCash, input.Note);
        if (!ok)
        {
            TempData["ToastError"] = err ?? "Không thể mở ca làm việc.";
            return Redirect("/staff/dashboard");
        }

        await _auditService.LogAsync(
            action: "OpenShift",
            entityName: "Shift",
            entityId: shift?.Id.ToString(),
            newValues: new { OpeningCash = input.OpeningCash, Note = input.Note });

        TempData["ToastSuccess"] = $"Đã mở ca trực thành công! Tiền mặt đầu ca: {FormatHelper.Vnd(input.OpeningCash)}.";
        return Redirect("/staff/dashboard");
    }

    [HttpPost("/staff/dashboard/close-shift")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CloseShift(CloseShiftInputDto input)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null)
            return Redirect("/login");

        if (!ModelState.IsValid)
        {
            TempData["ToastError"] = "Thông tin đóng ca không hợp lệ. Vui lòng kiểm tra số tiền thực đếm.";
            return Redirect("/staff/dashboard");
        }

        var (ok, shift, err) = await _shiftService.CloseShiftAsync(currentUser.Id, input.CountedCash, input.Note);
        if (!ok || shift == null)
        {
            TempData["ToastError"] = err ?? "Không thể đóng ca làm việc.";
            return Redirect("/staff/dashboard");
        }

        await _auditService.LogAsync(
            action: "CloseShift",
            entityName: "Shift",
            entityId: shift.Id.ToString(),
            newValues: new
            {
                shift.ExpectedCash,
                shift.CountedCash,
                shift.Difference
            });

        var diff = shift.Difference ?? 0m;
        var diffLabel = diff == 0m
            ? "Khớp két 100%"
            : (diff > 0 ? $"Dư {FormatHelper.Vnd(diff)}" : $"Lệch thiếu {FormatHelper.Vnd(Math.Abs(diff))}");

        TempData["ToastSuccess"] = $"Đã kết sổ đóng ca #{shift.Id}. Dự kiến: {FormatHelper.Vnd(shift.ExpectedCash)} | Thực đếm: {FormatHelper.Vnd(shift.CountedCash ?? 0)} ({diffLabel}).";
        return Redirect("/staff/dashboard");
    }
}
