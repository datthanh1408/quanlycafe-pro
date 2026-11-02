using CoffeeShop.Areas.Admin.Models;
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

namespace CoffeeShop.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = AppRoles.Admin)]
public class OrdersAdminController : Controller
{
    private readonly AppDbContext _db;
    private readonly IOrderService _orderService;
    private readonly IReportService _reportService;
    private readonly IAuditService _auditService;
    private readonly UserManager<ApplicationUser> _userManager;

    public OrdersAdminController(
        AppDbContext db,
        IOrderService orderService,
        IReportService reportService,
        IAuditService auditService,
        UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _orderService = orderService;
        _reportService = reportService;
        _auditService = auditService;
        _userManager = userManager;
    }

    [HttpGet("/admin/orders")]
    public async Task<IActionResult> Index(
        string? search = null,
        OrderStatus? status = null,
        PaymentStatus? paymentStatus = null,
        OrderType? orderType = null,
        string? staffId = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int page = 1)
    {
        const int pageSize = 15;
        page = Math.Max(1, page);

        var query = _db.Orders
            .AsNoTracking()
            .Include(o => o.Table)
            .Include(o => o.Staff)
            .Include(o => o.Items)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var kw = search.Trim();
            query = query.Where(o =>
                o.Code.Contains(kw) ||
                o.CustomerName.Contains(kw) ||
                (o.Phone != null && o.Phone.Contains(kw)));
        }

        if (status.HasValue)
            query = query.Where(o => o.Status == status.Value);

        if (paymentStatus.HasValue)
            query = query.Where(o => o.PaymentStatus == paymentStatus.Value);

        if (orderType.HasValue)
            query = query.Where(o => o.OrderType == orderType.Value);

        if (!string.IsNullOrWhiteSpace(staffId))
            query = query.Where(o => o.StaffId == staffId);

        if (fromDate.HasValue)
        {
            var fromUtc = FormatHelper.VnDateToUtcStart(fromDate.Value.Date);
            query = query.Where(o => o.CreatedAt >= fromUtc);
        }

        if (toDate.HasValue)
        {
            var toUtc = FormatHelper.VnDateToUtcStart(toDate.Value.Date.AddDays(1)).AddTicks(-1);
            query = query.Where(o => o.CreatedAt <= toUtc);
        }

        var totalItems = await query.CountAsync();
        var orders = await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var staffUsers = await _userManager.GetUsersInRoleAsync(AppRoles.Staff);
        var adminUsers = await _userManager.GetUsersInRoleAsync(AppRoles.Admin);
        var staffMembers = staffUsers.Concat(adminUsers)
            .Where(u => !u.IsDeleted)
            .GroupBy(u => u.Id)
            .Select(g => g.First())
            .OrderBy(u => u.FullName)
            .ToList();

        var qs = new List<string>();
        if (!string.IsNullOrWhiteSpace(search)) qs.Add($"search={Uri.EscapeDataString(search.Trim())}");
        if (status.HasValue) qs.Add($"status={status.Value}");
        if (paymentStatus.HasValue) qs.Add($"paymentStatus={paymentStatus.Value}");
        if (orderType.HasValue) qs.Add($"orderType={orderType.Value}");
        if (!string.IsNullOrWhiteSpace(staffId)) qs.Add($"staffId={Uri.EscapeDataString(staffId)}");
        if (fromDate.HasValue) qs.Add($"fromDate={fromDate.Value:yyyy-MM-dd}");
        if (toDate.HasValue) qs.Add($"toDate={toDate.Value:yyyy-MM-dd}");

        var baseQueryUrl = qs.Count > 0 ? $"/admin/orders?{string.Join("&", qs)}" : "/admin/orders";

        var vm = new OrdersAdminViewModel
        {
            Search = search,
            Status = status,
            PaymentStatus = paymentStatus,
            OrderType = orderType,
            StaffId = staffId,
            FromDate = fromDate,
            ToDate = toDate,
            StaffMembers = staffMembers,
            BaseQueryUrl = baseQueryUrl,
            PagedOrders = new PagedResult<Order>
            {
                Items = orders,
                Page = page,
                PageSize = pageSize,
                TotalItems = totalItems
            }
        };

        return View(vm);
    }

    [HttpGet("/admin/orders/export-excel")]
    public async Task<IActionResult> ExportExcel(DateTime? fromDate = null, DateTime? toDate = null, OrderStatus? status = null)
    {
        DateTime? fromUtc = fromDate.HasValue ? FormatHelper.VnDateToUtcStart(fromDate.Value.Date) : null;
        DateTime? toUtc = toDate.HasValue ? FormatHelper.VnDateToUtcStart(toDate.Value.Date.AddDays(1)).AddTicks(-1) : null;

        var bytes = await _reportService.ExportOrdersExcelAsync(fromUtc, toUtc, status);
        var fileName = $"DonHang_DatMocCoffee_{FormatHelper.VnNow():yyyyMMdd_HHmm}.xlsx";

        await _auditService.LogAsync("ExportOrdersExcel", "Order", null, null,
            new { FromDate = fromDate?.ToString("yyyy-MM-dd"), ToDate = toDate?.ToString("yyyy-MM-dd"), Status = status?.ToString() });

        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    [HttpGet("/admin/orders/detail/{id:int}")]
    [HttpGet("/admin/orders/{id:int}")]
    public async Task<IActionResult> Detail(int id)
    {
        var order = await _db.Orders
            .AsNoTracking()
            .Include(o => o.Table)
            .Include(o => o.Customer)
            .Include(o => o.Staff)
            .Include(o => o.Promotion)
            .Include(o => o.Items)
                .ThenInclude(i => i.Toppings)
            .Include(o => o.StatusHistories)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null)
        {
            TempData["ToastError"] = "Không tìm thấy đơn hàng.";
            return RedirectToAction(nameof(Index));
        }

        OrderStatus? nextStatus = null;
        OrderStatus? rollbackStatus = null;

        if (order.Status is not (OrderStatus.Completed or OrderStatus.Cancelled))
        {
            var flow = GetFlow(order.OrderType);
            var idx = flow.IndexOf(order.Status);
            if (idx >= 0 && idx + 1 < flow.Count)
            {
                nextStatus = flow[idx + 1];
            }
            if (idx > 0)
            {
                rollbackStatus = flow[idx - 1];
            }
        }

        var vm = new OrderAdminDetailViewModel
        {
            Order = order,
            NextStatus = nextStatus,
            RollbackStatus = rollbackStatus,
            RowVersionBase64 = Convert.ToBase64String(order.RowVersion)
        };

        return View("Detail", vm);
    }

    [HttpPost("/admin/orders/advance-status")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AdvanceStatus(int id, OrderStatus targetStatus, string? note = null, string? rowVersionBase64 = null)
    {
        var actorName = await ResolveAdminDisplayNameAsync();
        byte[]? expectedRv = TryDecodeRowVersion(rowVersionBase64);

        var (ok, err) = await _orderService.ChangeStatusAsync(
            orderId: id,
            targetStatus: targetStatus,
            actorName: actorName,
            isAdmin: true,
            note: note,
            expectedRowVersion: expectedRv);

        if (!ok)
        {
            TempData["ToastError"] = err ?? "Không thể chuyển trạng thái đơn hàng.";
        }
        else
        {
            await _auditService.LogAsync("AdvanceOrderStatus", "Order", id.ToString(), null,
                new { TargetStatus = targetStatus.ToString(), Note = note });
            TempData["ToastSuccess"] = $"Đã chuyển trạng thái đơn sang '{targetStatus.GetDisplayName()}'.";
        }

        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost("/admin/orders/rollback-status")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RollbackStatus(int id, OrderStatus targetStatus, string reason, string? rowVersionBase64 = null)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            TempData["ToastError"] = "Admin lùi trạng thái đơn bắt buộc phải nhập lý do.";
            return RedirectToAction(nameof(Detail), new { id });
        }

        var actorName = await ResolveAdminDisplayNameAsync();
        byte[]? expectedRv = TryDecodeRowVersion(rowVersionBase64);

        var (ok, err) = await _orderService.ChangeStatusAsync(
            orderId: id,
            targetStatus: targetStatus,
            actorName: actorName,
            isAdmin: true,
            note: $"[Admin Rollback] {reason.Trim()}",
            expectedRowVersion: expectedRv);

        if (!ok)
        {
            TempData["ToastError"] = err ?? "Không thể lùi trạng thái đơn hàng.";
        }
        else
        {
            await _auditService.LogAsync("RollbackOrderStatus", "Order", id.ToString(), null,
                new { RollbackTo = targetStatus.ToString(), Reason = reason.Trim() });
            TempData["ToastWarning"] = $"Đã lùi 1 bước trạng thái đơn về '{targetStatus.GetDisplayName()}'.";
        }

        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost("/admin/orders/confirm-payment")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmPayment(int id)
    {
        var actorName = await ResolveAdminDisplayNameAsync();
        var (ok, err) = await _orderService.ConfirmPaymentAsync(id, actorName);

        if (!ok)
        {
            TempData["ToastError"] = err ?? "Không thể xác nhận thanh toán.";
        }
        else
        {
            await _auditService.LogAsync("ConfirmOrderPayment", "Order", id.ToString(),
                new { PaymentStatus = "Unpaid" }, new { PaymentStatus = "Paid" });
            TempData["ToastSuccess"] = "Đã xác nhận thu tiền đơn hàng thành công.";
        }

        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost("/admin/orders/cancel")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id, string reason)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        var actorName = await ResolveAdminDisplayNameAsync();

        var (ok, err) = await _orderService.CancelOrderAsync(
            orderId: id,
            reason: reason,
            actorName: actorName,
            currentUserId: currentUser?.Id,
            isAdmin: true,
            hasStaffCancelClaim: true);

        if (!ok)
        {
            TempData["ToastError"] = err ?? "Không thể hủy đơn hàng.";
        }
        else
        {
            TempData["ToastWarning"] = "Đã hủy đơn hàng và cập nhật lịch sử trạng thái.";
        }

        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost("/admin/orders/void-refund")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VoidRefund(int id, string reason)
    {
        var actorName = await ResolveAdminDisplayNameAsync();
        var (ok, err) = await _orderService.VoidAndRefundCompletedOrderAsync(id, reason, actorName);

        if (!ok)
        {
            TempData["ToastError"] = err ?? "Không thể hoàn tiền / void đơn hoàn thành.";
        }
        else
        {
            TempData["ToastWarning"] = "Đã thực hiện Hoàn tiền (Void) cho đơn hàng hoàn thành.";
        }

        return RedirectToAction(nameof(Detail), new { id });
    }

    private async Task<string> ResolveAdminDisplayNameAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (!string.IsNullOrWhiteSpace(user?.FullName))
            return $"{user.FullName} (Admin)";
        return User.Identity?.Name ?? "Admin";
    }

    private static byte[]? TryDecodeRowVersion(string? b64)
    {
        if (string.IsNullOrWhiteSpace(b64)) return null;
        try { return Convert.FromBase64String(b64); }
        catch { return null; }
    }

    private static List<OrderStatus> GetFlow(OrderType type)
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
}
