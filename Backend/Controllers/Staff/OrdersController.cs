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
public class OrdersController : Controller
{
    private readonly AppDbContext _db;
    private readonly IOrderService _orderService;
    private readonly ISettingService _settingService;
    private readonly UserManager<ApplicationUser> _userManager;

    public OrdersController(
        AppDbContext db,
        IOrderService orderService,
        ISettingService settingService,
        UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _orderService = orderService;
        _settingService = settingService;
        _userManager = userManager;
    }

    public static List<OrderStatus> GetFlowForOrderType(OrderType type)
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

    public static OrderStatus? GetNextForwardStatus(OrderType type, OrderStatus current)
    {
        if (current is OrderStatus.Completed or OrderStatus.Cancelled)
            return null;

        var flow = GetFlowForOrderType(type);
        var idx = flow.IndexOf(current);
        if (idx < 0 || idx + 1 >= flow.Count)
            return null;

        return flow[idx + 1];
    }

    [HttpGet("/staff/orders")]
    public async Task<IActionResult> Index(
        OrderStatus? status = null,
        string? q = null,
        DateTime? date = null,
        int page = 1)
    {
        const int pageSize = 12;
        page = Math.Max(1, page);

        var baseQuery = _db.Orders.AsNoTracking().AsQueryable();

        var cleanQ = q?.Trim();
        if (!string.IsNullOrWhiteSpace(cleanQ))
        {
            baseQuery = baseQuery.Where(o =>
                o.Code.Contains(cleanQ) ||
                o.CustomerName.Contains(cleanQ) ||
                (o.Phone != null && o.Phone.Contains(cleanQ)));
        }

        string? dateStr = null;
        if (date.HasValue)
        {
            dateStr = date.Value.ToString("yyyy-MM-dd");
            var startUtc = FormatHelper.VnDateToUtcStart(date.Value);
            var endUtc = startUtc.AddDays(1);
            baseQuery = baseQuery.Where(o => o.CreatedAt >= startUtc && o.CreatedAt < endUtc);
        }

        // Thống kê số lượng từng tab theo bộ lọc ngày/tìm kiếm hiện tại
        var groupedCounts = await baseQuery
            .GroupBy(o => o.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        var statusCounts = Enum.GetValues<OrderStatus>().ToDictionary(s => s, _ => 0);
        foreach (var g in groupedCounts)
        {
            statusCounts[g.Status] = g.Count;
        }
        var allCount = statusCounts.Values.Sum();

        var filteredQuery = status.HasValue
            ? baseQuery.Where(o => o.Status == status.Value)
            : baseQuery;

        var totalItems = await filteredQuery.CountAsync();

        var rawItems = await filteredQuery
            .Include(o => o.Table)
            .Include(o => o.Items)
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var mappedItems = rawItems.Select(o => new StaffOrderListItemVm
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
            NextForwardStatus = GetNextForwardStatus(o.OrderType, o.Status),
            RowVersionBase64 = o.RowVersion.Length > 0 ? Convert.ToBase64String(o.RowVersion) : null
        }).ToList();

        var queryParts = new List<string>();
        if (status.HasValue) queryParts.Add($"status={status.Value}");
        if (!string.IsNullOrWhiteSpace(cleanQ)) queryParts.Add($"q={Uri.EscapeDataString(cleanQ)}");
        if (!string.IsNullOrWhiteSpace(dateStr)) queryParts.Add($"date={dateStr}");

        var baseQueryUrl = queryParts.Count > 0
            ? $"/staff/orders?{string.Join("&", queryParts)}"
            : "/staff/orders";

        var vm = new StaffOrderListVm
        {
            Orders = new PagedResult<StaffOrderListItemVm>
            {
                Items = mappedItems,
                Page = page,
                PageSize = pageSize,
                TotalItems = totalItems
            },
            CurrentStatus = status,
            SearchKeyword = cleanQ,
            DateFilter = dateStr,
            BaseQueryUrl = baseQueryUrl,
            StatusCounts = statusCounts,
            AllCount = allCount
        };

        return View(vm);
    }

    [HttpGet("/staff/orders/detail/{id:int}")]
    public async Task<IActionResult> Detail(int id)
    {
        var order = await _db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
                .ThenInclude(i => i.Toppings)
            .Include(o => o.Table)
            .Include(o => o.Promotion)
            .Include(o => o.Customer)
            .Include(o => o.Staff)
            .Include(o => o.StatusHistories)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null)
            return NotFound();

        var settings = await _settingService.GetStoreSettingsAsync();
        var flow = GetFlowForOrderType(order.OrderType);
        var currentIdx = flow.IndexOf(order.Status);

        OrderStatus? nextForward = null;
        OrderStatus? prevRollback = null;

        if (order.Status is not (OrderStatus.Completed or OrderStatus.Cancelled) && currentIdx >= 0)
        {
            if (currentIdx + 1 < flow.Count)
                nextForward = flow[currentIdx + 1];

            if (currentIdx - 1 >= 0)
                prevRollback = flow[currentIdx - 1];
        }

        bool isAdmin = User.IsInRole(AppRoles.Admin);
        bool hasCancelPolicy = AppPolicies.HasAdminOrClaim(User, StaffPermissions.CancelOrder);
        bool canCancelThisOrder = hasCancelPolicy &&
                                  order.Status != OrderStatus.Completed &&
                                  order.Status != OrderStatus.Cancelled &&
                                  (isAdmin || order.Status is OrderStatus.Pending or OrderStatus.Confirmed);

        var vm = new StaffOrderDetailVm
        {
            Order = order,
            StoreSettings = settings,
            StatusFlow = flow,
            NextForwardStatus = nextForward,
            PrevRollbackStatus = isAdmin ? prevRollback : null,
            IsAdmin = isAdmin,
            CanCancelThisOrder = canCancelThisOrder,
            RowVersionBase64 = order.RowVersion.Length > 0 ? Convert.ToBase64String(order.RowVersion) : string.Empty
        };

        return View(vm);
    }

    [HttpPost("/staff/orders/change-status")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeStatus(
        int id,
        OrderStatus targetStatus,
        string? note = null,
        string? rowVersionBase64 = null,
        string? returnUrl = null)
    {
        var actorName = await ResolveActorNameAsync();
        bool isAdmin = User.IsInRole(AppRoles.Admin);

        byte[]? expectedRv = null;
        if (!string.IsNullOrWhiteSpace(rowVersionBase64))
        {
            try
            {
                expectedRv = Convert.FromBase64String(rowVersionBase64);
            }
            catch
            {
                expectedRv = null;
            }
        }

        var (ok, err) = await _orderService.ChangeStatusAsync(id, targetStatus, actorName, isAdmin, note, expectedRv);
        if (ok)
        {
            TempData["ToastSuccess"] = $"Đã cập nhật đơn sang trạng thái '{targetStatus.GetDisplayName()}'.";
        }
        else
        {
            TempData["ToastError"] = err ?? "Không thể chuyển trạng thái đơn hàng.";
        }

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return LocalRedirect(returnUrl);

        return Redirect($"/staff/orders/detail/{id}");
    }

    [HttpPost("/staff/orders/confirm-payment")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmPayment(int id, string? returnUrl = null)
    {
        var actorName = await ResolveActorNameAsync();
        var (ok, err) = await _orderService.ConfirmPaymentAsync(id, actorName);

        if (ok)
        {
            TempData["ToastSuccess"] = "Đã xác nhận thu tiền / nhận chuyển khoản thành công!";
        }
        else
        {
            TempData["ToastError"] = err ?? "Không thể xác nhận thanh toán.";
        }

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return LocalRedirect(returnUrl);

        return Redirect($"/staff/orders/detail/{id}");
    }

    [HttpPost("/staff/orders/cancel")]
    [Authorize(Policy = AppPolicies.CanCancelOrder)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id, string reason, string? returnUrl = null)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        var actorName = currentUser?.FullName ?? User.Identity?.Name ?? "Nhân viên";
        bool isAdmin = User.IsInRole(AppRoles.Admin);
        bool hasStaffCancelClaim = AppPolicies.HasAdminOrClaim(User, StaffPermissions.CancelOrder);

        var (ok, err) = await _orderService.CancelOrderAsync(
            id,
            reason,
            actorName,
            currentUser?.Id,
            isAdmin,
            hasStaffCancelClaim);

        if (ok)
        {
            TempData["ToastSuccess"] = "Đã hủy đơn hàng thành công.";
        }
        else
        {
            TempData["ToastError"] = err ?? "Không thể hủy đơn hàng.";
        }

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return LocalRedirect(returnUrl);

        return Redirect($"/staff/orders/detail/{id}");
    }

    [HttpGet("/staff/orders/receipt/{id:int}")]
    public async Task<IActionResult> Receipt(int id, bool fromPos = false)
    {
        var order = await _db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
                .ThenInclude(i => i.Toppings)
            .Include(o => o.Table)
            .Include(o => o.Promotion)
            .Include(o => o.Staff)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null)
            return NotFound();

        var settings = await _settingService.GetStoreSettingsAsync();
        var cashierName = order.Staff?.FullName ?? await ResolveActorNameAsync();

        var vm = new StaffOrderReceiptVm
        {
            Order = order,
            StoreSettings = settings,
            CashierName = cashierName,
            FromPos = fromPos
        };

        return View(vm);
    }

    private async Task<string> ResolveActorNameAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        return !string.IsNullOrWhiteSpace(user?.FullName)
            ? user.FullName
            : (User.Identity?.Name ?? "Nhân viên");
    }
}
