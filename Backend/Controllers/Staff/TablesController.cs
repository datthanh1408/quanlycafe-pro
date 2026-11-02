using System.Text.Json;
using CoffeeShop.Areas.Staff.Models;
using CoffeeShop.Authorization;
using CoffeeShop.Data;
using CoffeeShop.Helpers;
using CoffeeShop.Hubs;
using CoffeeShop.Models.Entities;
using CoffeeShop.Models.Enums;
using CoffeeShop.Models.ViewModels;
using CoffeeShop.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShop.Areas.Staff.Controllers;

[Area("Staff")]
[Authorize(Policy = AppPolicies.StaffOrAdmin)]
public class TablesController : Controller
{
    private static readonly JsonSerializerOptions CamelJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly AppDbContext _db;
    private readonly ITableService _tableService;
    private readonly ISettingService _settingService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IHubContext<OrderHub> _hub;

    public TablesController(
        AppDbContext db,
        ITableService tableService,
        ISettingService settingService,
        UserManager<ApplicationUser> userManager,
        IHubContext<OrderHub> hub)
    {
        _db = db;
        _tableService = tableService;
        _settingService = settingService;
        _userManager = userManager;
        _hub = hub;
    }

    [HttpGet("/staff/tables")]
    public async Task<IActionResult> Index(int? selectedId = null)
    {
        var tables = await _tableService.GetTablesFor3DAsync();

        var vm = new StaffTablesVm
        {
            Tables = tables,
            TablesJson = JsonSerializer.Serialize(tables, CamelJsonOptions),
            SelectedTableId = selectedId,
            AvailableCount = tables.Count(t => t.Status == TableStatus.Available),
            OccupiedCount = tables.Count(t => t.Status == TableStatus.Occupied),
            ReservedCount = tables.Count(t => t.Status == TableStatus.Reserved)
        };

        return View(vm);
    }

    [HttpPost("/staff/tables/change-status")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeStatus(int tableId, TableStatus newStatus)
    {
        var actorName = await ResolveActorNameAsync();
        var (ok, err) = await _tableService.ChangeTableStatusAsync(tableId, newStatus, actorName);

        if (ok)
        {
            TempData["ToastSuccess"] = $"Đã cập nhật trạng thái bàn sang '{newStatus.GetDisplayName()}'.";
        }
        else
        {
            TempData["ToastError"] = err ?? "Không thể cập nhật trạng thái bàn.";
        }

        return Redirect($"/staff/tables?selectedId={tableId}");
    }

    [HttpPost("/staff/tables/transfer")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Transfer(int fromTableId, int toTableId)
    {
        var actorName = await ResolveActorNameAsync();
        var (ok, err) = await _tableService.TransferTableAsync(fromTableId, toTableId, actorName);

        if (ok)
        {
            TempData["ToastSuccess"] = "Chuyển bàn thành công! Sơ đồ 3D đã được đồng bộ.";
            return Redirect($"/staff/tables?selectedId={toTableId}");
        }

        TempData["ToastError"] = err ?? "Không thể chuyển bàn.";
        return Redirect($"/staff/tables?selectedId={fromTableId}");
    }

    [HttpPost("/staff/tables/merge")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Merge(int sourceTableId, int targetTableId)
    {
        var actorName = await ResolveActorNameAsync();
        var (ok, err) = await _tableService.MergeTablesAsync(sourceTableId, targetTableId, actorName);

        if (ok)
        {
            TempData["ToastSuccess"] = "Gộp bàn thành công! Các món đã được gộp về bàn đích.";
            return Redirect($"/staff/tables?selectedId={targetTableId}");
        }

        TempData["ToastError"] = err ?? "Không thể gộp bàn.";
        return Redirect($"/staff/tables?selectedId={sourceTableId}");
    }

    [HttpGet("/staff/reservations")]
    public async Task<IActionResult> Reservations(
        ReservationStatus? status = null,
        DateTime? date = null,
        string? q = null,
        int page = 1)
    {
        const int pageSize = 12;
        page = Math.Max(1, page);

        var todayStartUtc = FormatHelper.VnDateToUtcStart(FormatHelper.VnNow());
        var todayEndUtc = todayStartUtc.AddDays(1);

        var pendingCount = await _db.TableReservations
            .AsNoTracking()
            .CountAsync(r => r.Status == ReservationStatus.Pending);

        var confirmedTodayCount = await _db.TableReservations
            .AsNoTracking()
            .CountAsync(r => r.Status == ReservationStatus.Confirmed &&
                             r.ReservedAt >= todayStartUtc &&
                             r.ReservedAt < todayEndUtc);

        var query = _db.TableReservations
            .AsNoTracking()
            .Include(r => r.Table)
            .AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(r => r.Status == status.Value);
        }

        string? dateStr = null;
        if (date.HasValue)
        {
            dateStr = date.Value.ToString("yyyy-MM-dd");
            var startUtc = FormatHelper.VnDateToUtcStart(date.Value);
            var endUtc = startUtc.AddDays(1);
            query = query.Where(r => r.ReservedAt >= startUtc && r.ReservedAt < endUtc);
        }

        var cleanQ = q?.Trim();
        if (!string.IsNullOrWhiteSpace(cleanQ))
        {
            query = query.Where(r =>
                r.CustomerName.Contains(cleanQ) ||
                r.Phone.Contains(cleanQ) ||
                r.Table.Code.Contains(cleanQ));
        }

        var totalItems = await query.CountAsync();

        var items = await query
            .OrderBy(r => r.Status == ReservationStatus.Pending ? 0 : (r.Status == ReservationStatus.Confirmed ? 1 : 2))
            .ThenByDescending(r => r.ReservedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new StaffReservationItemVm
            {
                Id = r.Id,
                TableId = r.TableId,
                TableCode = r.Table.Code,
                Zone = r.Table.Zone,
                TableCapacity = r.Table.Capacity,
                CustomerName = r.CustomerName,
                Phone = r.Phone,
                PartySize = r.PartySize,
                ReservedAt = r.ReservedAt,
                Status = r.Status,
                Note = r.Note,
                CreatedAt = r.CreatedAt
            })
            .ToListAsync();

        var queryParts = new List<string>();
        if (status.HasValue) queryParts.Add($"status={status.Value}");
        if (!string.IsNullOrWhiteSpace(dateStr)) queryParts.Add($"date={dateStr}");
        if (!string.IsNullOrWhiteSpace(cleanQ)) queryParts.Add($"q={Uri.EscapeDataString(cleanQ)}");

        var baseQueryUrl = queryParts.Count > 0
            ? $"/staff/reservations?{string.Join("&", queryParts)}"
            : "/staff/reservations";

        var vm = new StaffReservationsVm
        {
            Reservations = new PagedResult<StaffReservationItemVm>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalItems = totalItems
            },
            CurrentStatus = status,
            DateFilter = dateStr,
            SearchKeyword = cleanQ,
            BaseQueryUrl = baseQueryUrl,
            PendingCount = pendingCount,
            ConfirmedTodayCount = confirmedTodayCount
        };

        return View("Reservations", vm);
    }

    [HttpPost("/staff/reservations/update-status")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateReservationStatus(
        int id,
        ReservationStatus newStatus,
        bool openPos = false,
        string? returnUrl = null)
    {
        var reservation = await _db.TableReservations
            .Include(r => r.Table)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (reservation == null)
        {
            TempData["ToastError"] = "Không tìm thấy lịch đặt bàn.";
            return Redirect("/staff/reservations");
        }

        reservation.Status = newStatus;

        var settings = await _settingService.GetStoreSettingsAsync();
        var nowUtc = DateTime.UtcNow;
        var hasOpenOrder = await _db.Orders.AnyAsync(o =>
            o.TableId == reservation.TableId &&
            o.Status != OrderStatus.Completed &&
            o.Status != OrderStatus.Cancelled);

        if (!hasOpenOrder && reservation.Table != null)
        {
            if (newStatus == ReservationStatus.Confirmed)
            {
                if (reservation.ReservedAt >= nowUtc.AddMinutes(-15) &&
                    reservation.ReservedAt <= nowUtc.AddMinutes(settings.ReservationHoldMinutes + 30))
                {
                    reservation.Table.Status = TableStatus.Reserved;
                }
            }
            else if (newStatus is ReservationStatus.Cancelled or ReservationStatus.Completed)
            {
                if (reservation.Table.Status == TableStatus.Reserved)
                {
                    reservation.Table.Status = TableStatus.Available;
                }
            }
        }

        await _db.SaveChangesAsync();

        await _hub.Clients.All.SendAsync("TableStatusChanged", new
        {
            tableId = reservation.TableId,
            reservationUpdated = true
        });

        TempData["ToastSuccess"] = $"Đã cập nhật lịch đặt bàn {reservation.Table?.Code} sang '{newStatus.GetDisplayName()}'.";

        if (openPos && newStatus == ReservationStatus.Completed)
        {
            var encodedName = Uri.EscapeDataString(reservation.CustomerName);
            var encodedPhone = Uri.EscapeDataString(reservation.Phone);
            return Redirect($"/staff/pos?tableId={reservation.TableId}&customerName={encodedName}&phone={encodedPhone}");
        }

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return LocalRedirect(returnUrl);

        return Redirect("/staff/reservations");
    }

    private async Task<string> ResolveActorNameAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        return !string.IsNullOrWhiteSpace(user?.FullName)
            ? user.FullName
            : (User.Identity?.Name ?? "Nhân viên");
    }
}
