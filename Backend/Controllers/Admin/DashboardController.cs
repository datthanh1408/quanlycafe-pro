using CoffeeShop.Areas.Admin.Models;
using CoffeeShop.Authorization;
using CoffeeShop.Helpers;
using CoffeeShop.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoffeeShop.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = AppRoles.Admin)]
public class DashboardController : Controller
{
    private readonly IReportService _reportService;

    public DashboardController(IReportService reportService)
    {
        _reportService = reportService;
    }

    [HttpGet("/admin")]
    [HttpGet("/admin/dashboard")]
    public async Task<IActionResult> Index(string range = "30d", DateTime? fromDate = null, DateTime? toDate = null)
    {
        var vnToday = FormatHelper.VnNow().Date;
        var normalizedRange = (range ?? "30d").Trim().ToLowerInvariant();

        DateTime startVn;
        DateTime endVn;
        string periodLabel;

        switch (normalizedRange)
        {
            case "today":
                startVn = vnToday;
                endVn = vnToday;
                periodLabel = $"Hôm nay ({vnToday:dd/MM/yyyy})";
                break;
            case "7d":
                startVn = vnToday.AddDays(-6);
                endVn = vnToday;
                periodLabel = $"7 ngày gần nhất ({startVn:dd/MM} – {endVn:dd/MM/yyyy})";
                break;
            case "custom":
                startVn = (fromDate ?? vnToday.AddDays(-29)).Date;
                endVn = (toDate ?? vnToday).Date;
                if (endVn < startVn)
                {
                    (startVn, endVn) = (endVn, startVn);
                }
                periodLabel = $"Tùy chọn ({startVn:dd/MM/yyyy} – {endVn:dd/MM/yyyy})";
                break;
            default:
                normalizedRange = "30d";
                startVn = vnToday.AddDays(-29);
                endVn = vnToday;
                periodLabel = $"30 ngày gần nhất ({startVn:dd/MM} – {endVn:dd/MM/yyyy})";
                break;
        }

        var fromUtc = FormatHelper.VnDateToUtcStart(startVn);
        var toUtc = FormatHelper.VnDateToUtcStart(endVn.AddDays(1)).AddTicks(-1);

        var summary = await _reportService.GetAdminSummaryAsync(fromUtc, toUtc);

        var vm = new AdminDashboardViewModel
        {
            Range = normalizedRange,
            FromDate = startVn,
            ToDate = endVn,
            PeriodLabel = periodLabel,
            Summary = summary
        };

        return View(vm);
    }
}
