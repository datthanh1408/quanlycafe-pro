using System.Globalization;
using System.Text.RegularExpressions;
using CoffeeShop.Areas.Admin.Models;
using CoffeeShop.Authorization;
using CoffeeShop.Data;
using CoffeeShop.Helpers;
using CoffeeShop.Models.Entities;
using CoffeeShop.Models.Enums;
using CoffeeShop.Models.ViewModels;
using CoffeeShop.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShop.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = AppRoles.Admin)]
public class ReportsSettingsController : Controller
{
    private readonly AppDbContext _db;
    private readonly IReportService _reportService;
    private readonly ISettingService _settingService;
    private readonly IAuditService _auditService;
    private readonly IWebHostEnvironment _env;

    public ReportsSettingsController(
        AppDbContext db,
        IReportService reportService,
        ISettingService settingService,
        IAuditService auditService,
        IWebHostEnvironment env)
    {
        _db = db;
        _reportService = reportService;
        _settingService = settingService;
        _auditService = auditService;
        _env = env;
    }

    // =========================================================================
    // 1. REPORTS (/admin/reports)
    // =========================================================================
    [HttpGet("/admin/reports")]
    public async Task<IActionResult> Reports(string preset = "month", DateTime? fromDate = null, DateTime? toDate = null)
    {
        var (normalizedPreset, startVn, endVn) = ResolveReportDateRange(preset, fromDate, toDate);
        var fromUtc = FormatHelper.VnDateToUtcStart(startVn);
        var toUtc = FormatHelper.VnDateToUtcStart(endVn.AddDays(1)).AddTicks(-1);

        var summary = await _reportService.GetAdminSummaryAsync(fromUtc, toUtc);

        var ingredients = await _db.Ingredients
            .AsNoTracking()
            .Where(i => !i.IsDeleted)
            .OrderBy(i => i.Code)
            .ToListAsync();

        var txInPeriod = await _db.StockTransactions
            .AsNoTracking()
            .Where(t => t.CreatedAt >= fromUtc && t.CreatedAt <= toUtc)
            .ToListAsync();

        var txByIng = txInPeriod.GroupBy(t => t.IngredientId).ToDictionary(g => g.Key, g => g.ToList());

        var movements = ingredients.Select(ing =>
        {
            txByIng.TryGetValue(ing.Id, out var list);
            list ??= new List<StockTransaction>();

            return new StockMovementSummaryVm
            {
                IngredientId = ing.Id,
                Code = ing.Code,
                Name = ing.Name,
                Unit = ing.Unit,
                ImportedQty = list.Where(x => x.Type == StockTransactionType.Import).Sum(x => x.Quantity),
                SaleConsumedQty = Math.Abs(list.Where(x => x.Type == StockTransactionType.Sale).Sum(x => x.Quantity)),
                ExportAndWastageQty = Math.Abs(list.Where(x => x.Type is StockTransactionType.Export or StockTransactionType.Wastage).Sum(x => x.Quantity)),
                AdjustDeltaQty = list.Where(x => x.Type == StockTransactionType.Adjust).Sum(x => x.Quantity),
                CurrentStock = ing.StockQty,
                MinThreshold = ing.MinThreshold
            };
        }).ToList();

        var vm = new ReportsAdminViewModel
        {
            Preset = normalizedPreset,
            FromDate = startVn,
            ToDate = endVn,
            Summary = summary,
            StockMovements = movements
        };

        return View("Reports", vm);
    }

    [HttpGet("/admin/reports/export-excel")]
    public async Task<IActionResult> ExportComprehensiveExcel(string preset = "month", DateTime? fromDate = null, DateTime? toDate = null)
    {
        var (_, startVn, endVn) = ResolveReportDateRange(preset, fromDate, toDate);
        var fromUtc = FormatHelper.VnDateToUtcStart(startVn);
        var toUtc = FormatHelper.VnDateToUtcStart(endVn.AddDays(1)).AddTicks(-1);

        var bytes = await _reportService.ExportComprehensiveReportExcelAsync(fromUtc, toUtc);
        var fileName = $"BaoCaoTongHop_DatMocCoffee_{startVn:yyyyMMdd}_{endVn:yyyyMMdd}.xlsx";

        await _auditService.LogAsync("ExportComprehensiveReportExcel", "Report", null, null,
            new { From = startVn.ToString("yyyy-MM-dd"), To = endVn.ToString("yyyy-MM-dd") });

        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    private static (string Preset, DateTime StartVn, DateTime EndVn) ResolveReportDateRange(string? preset, DateTime? fromDate, DateTime? toDate)
    {
        var vnToday = FormatHelper.VnNow().Date;
        var norm = (preset ?? "month").Trim().ToLowerInvariant();

        if (fromDate.HasValue && toDate.HasValue && norm == "custom")
        {
            var s = fromDate.Value.Date;
            var e = toDate.Value.Date;
            if (e < s) (s, e) = (e, s);
            return ("custom", s, e);
        }

        return norm switch
        {
            "today" => ("today", vnToday, vnToday),
            "7d" => ("7d", vnToday.AddDays(-6), vnToday),
            "year" => ("year", new DateTime(vnToday.Year, 1, 1), vnToday),
            "custom" when fromDate.HasValue || toDate.HasValue => (
                "custom",
                (fromDate ?? vnToday.AddDays(-29)).Date,
                (toDate ?? vnToday).Date),
            _ => ("month", new DateTime(vnToday.Year, vnToday.Month, 1), vnToday)
        };
    }

    // =========================================================================
    // 2. SYSTEM SETTINGS (/admin/settings)
    // =========================================================================
    [HttpGet("/admin/settings")]
    public async Task<IActionResult> Settings(string tab = "tab-store")
    {
        var dto = await _settingService.GetStoreSettingsAsync();
        var methods = await _settingService.GetAllPaymentMethodsAsync();

        var vm = new SettingsAdminViewModel
        {
            PaymentMethods = methods,
            IsQrConfigured = dto.IsQrConfigured,
            Form = new SettingsFormModel
            {
                ActiveTab = string.IsNullOrWhiteSpace(tab) ? "tab-store" : tab,
                ShopName = dto.ShopName,
                Slogan = dto.Slogan,
                BusinessType = dto.BusinessType,
                OpeningHours = dto.OpeningHours,
                Address = dto.Address,
                ExistingLogoUrl = dto.LogoUrl,
                PrimaryColor = dto.PrimaryColor,
                SecondaryColor = dto.SecondaryColor,
                AccentColor = dto.AccentColor,
                Phone = dto.Phone,
                Email = dto.Email,
                FacebookUrl = dto.FacebookUrl,
                ZaloUrl = dto.ZaloUrl,
                GoogleMapsEmbedUrl = dto.GoogleMapsEmbedUrl,
                BankName = dto.BankName,
                BankAccountNumber = dto.BankAccountNumber,
                BankAccountHolder = dto.BankAccountHolder,
                ExistingQrImageUrl = dto.QrImageUrl,
                ShippingFee = dto.ShippingFee,
                FreeShippingThreshold = dto.FreeShippingThreshold,
                LoyaltyEarnPerVnd = dto.LoyaltyEarnPerVnd,
                LoyaltyRedeemValueVnd = dto.LoyaltyRedeemValueVnd,
                ReservationHoldMinutes = dto.ReservationHoldMinutes
            }
        };

        return View("Settings", vm);
    }

    [HttpPost("/admin/settings/save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveSettings(SettingsFormModel form)
    {
        var current = await _settingService.GetStoreSettingsAsync();

        string logoUrl = current.LogoUrl;
        if (form.LogoFile != null && form.LogoFile.Length > 0)
        {
            var upLogo = await ImageUploadHelper.SaveImageAsync(form.LogoFile, _env, "branding", current.LogoUrl);
            if (!upLogo.Success)
            {
                TempData["ToastError"] = $"Lỗi tải Logo: {upLogo.ErrorMessage}";
                return RedirectToAction(nameof(Settings), new { tab = form.ActiveTab });
            }
            logoUrl = upLogo.RelativeUrl!;
        }

        string qrUrl = current.QrImageUrl;
        if (form.QrImageFile != null && form.QrImageFile.Length > 0)
        {
            var upQr = await ImageUploadHelper.SaveImageAsync(form.QrImageFile, _env, "branding", current.QrImageUrl);
            if (!upQr.Success)
            {
                TempData["ToastError"] = $"Lỗi tải ảnh mã QR: {upQr.ErrorMessage}";
                return RedirectToAction(nameof(Settings), new { tab = form.ActiveTab });
            }
            qrUrl = upQr.RelativeUrl!;
        }

        var primaryHex = SanitizeHexColor(form.PrimaryColor, "#6F4E37");
        var secondaryHex = SanitizeHexColor(form.SecondaryColor, "#F6EFE7");
        var accentHex = SanitizeHexColor(form.AccentColor, "#C8782A");
        var cleanMapUrl = SanitizeGoogleMapsEmbedUrl(form.GoogleMapsEmbedUrl);

        var updates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ShopName"] = string.IsNullOrWhiteSpace(form.ShopName) ? "Đạt Mộc Coffee" : form.ShopName.Trim(),
            ["Slogan"] = (form.Slogan ?? string.Empty).Trim(),
            ["BusinessType"] = string.IsNullOrWhiteSpace(form.BusinessType) ? "Cafe & Bakery" : form.BusinessType.Trim(),
            ["OpeningHours"] = (form.OpeningHours ?? string.Empty).Trim(),
            ["Address"] = (form.Address ?? string.Empty).Trim(),
            ["LogoUrl"] = logoUrl,
            ["PrimaryColor"] = primaryHex,
            ["SecondaryColor"] = secondaryHex,
            ["AccentColor"] = accentHex,
            ["Phone"] = (form.Phone ?? string.Empty).Trim(),
            ["Email"] = (form.Email ?? string.Empty).Trim(),
            ["FacebookUrl"] = SanitizeExternalUrl(form.FacebookUrl),
            ["ZaloUrl"] = SanitizeExternalUrl(form.ZaloUrl),
            ["GoogleMapsEmbedUrl"] = cleanMapUrl,
            ["BankName"] = (form.BankName ?? string.Empty).Trim(),
            ["BankAccountNumber"] = (form.BankAccountNumber ?? string.Empty).Trim(),
            ["BankAccountHolder"] = (form.BankAccountHolder ?? string.Empty).Trim(),
            ["QrImageUrl"] = qrUrl,
            ["ShippingFee"] = Math.Max(0m, Math.Round(form.ShippingFee, 0)).ToString(CultureInfo.InvariantCulture),
            ["FreeShippingThreshold"] = Math.Max(0m, Math.Round(form.FreeShippingThreshold, 0)).ToString(CultureInfo.InvariantCulture),
            ["LoyaltyEarnPerVnd"] = Math.Max(1000, form.LoyaltyEarnPerVnd).ToString(CultureInfo.InvariantCulture),
            ["LoyaltyRedeemValueVnd"] = Math.Max(1, form.LoyaltyRedeemValueVnd).ToString(CultureInfo.InvariantCulture),
            ["ReservationHoldMinutes"] = Math.Clamp(form.ReservationHoldMinutes, 5, 240).ToString(CultureInfo.InvariantCulture)
        };

        await _settingService.UpdateSettingsAsync(updates);

        await _auditService.LogAsync("UpdateSystemSettings", "SystemSetting", form.ActiveTab,
            new { current.ShopName, current.PrimaryColor, current.ShippingFee },
            new { ShopName = updates["ShopName"], PrimaryColor = primaryHex, ShippingFee = updates["ShippingFee"] });

        TempData["ToastSuccess"] = "Đã lưu cấu hình hệ thống thành công.";
        return RedirectToAction(nameof(Settings), new { tab = form.ActiveTab });
    }

    [HttpPost("/admin/settings/toggle-payment")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TogglePaymentMethod(string code, bool isActive)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            TempData["ToastError"] = "Mã phương thức thanh toán không hợp lệ.";
            return RedirectToAction(nameof(Settings), new { tab = "tab-payment" });
        }

        var normalizedCode = code.Trim().ToUpperInvariant();
        await _settingService.TogglePaymentMethodAsync(normalizedCode, isActive);

        await _auditService.LogAsync("TogglePaymentMethod", "PaymentMethodConfig", normalizedCode,
            null, new { Code = normalizedCode, IsActive = isActive });

        TempData["ToastSuccess"] = $"Đã {(isActive ? "bật" : "tắt")} phương thức thanh toán '{normalizedCode}'.";
        return RedirectToAction(nameof(Settings), new { tab = "tab-payment" });
    }

    private static string SanitizeHexColor(string? input, string fallback)
    {
        if (string.IsNullOrWhiteSpace(input)) return fallback;
        var trimmed = input.Trim();
        return Regex.IsMatch(trimmed, @"^#[0-9A-Fa-f]{6}$") ? trimmed.ToUpperInvariant() : fallback;
    }

    private static string SanitizeExternalUrl(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var trimmed = input.Trim();
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return trimmed;
        }
        return string.Empty;
    }

    private static string SanitizeGoogleMapsEmbedUrl(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        var text = raw.Trim();

        // Nếu Admin dán nguyên thẻ <iframe src="...">, trích xuất thuộc tính src
        var srcMatch = Regex.Match(text, @"src\s*=\s*[""']([^""']+)[""']", RegexOptions.IgnoreCase);
        if (srcMatch.Success)
        {
            text = srcMatch.Groups[1].Value.Trim();
        }

        if (Uri.TryCreate(text, UriKind.Absolute, out var uri) &&
            uri.Scheme == Uri.UriSchemeHttps &&
            (uri.Host.EndsWith("google.com", StringComparison.OrdinalIgnoreCase) ||
             uri.Host.EndsWith("google.com.vn", StringComparison.OrdinalIgnoreCase)))
        {
            return text;
        }

        return string.Empty;
    }

    // =========================================================================
    // 3. AUDIT LOGS (/admin/audit-logs)
    // =========================================================================
    [HttpGet("/admin/audit-logs")]
    public async Task<IActionResult> AuditLogs(
        string? userEmail = null,
        string? actionFilter = null,
        string? action = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int page = 1)
    {
        // Lưu ý: Trong MVC, tham số tên "action" có thể bị RouteData["action"] = "AuditLogs" điền vào nếu query string không có,
        // nên ta kiểm tra trực tiếp Request.Query["action"] để lọc chuẩn xác.
        string? effectiveAction = !string.IsNullOrWhiteSpace(actionFilter)
            ? actionFilter.Trim()
            : (Request.Query.ContainsKey("action") ? Request.Query["action"].ToString().Trim() : null);

        const int pageSize = 20;
        page = Math.Max(1, page);

        var query = _db.AuditLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(userEmail))
        {
            var kw = userEmail.Trim();
            query = query.Where(a => a.UserEmail != null && a.UserEmail.Contains(kw));
        }

        if (!string.IsNullOrWhiteSpace(effectiveAction))
        {
            query = query.Where(a => a.Action == effectiveAction || a.Action.Contains(effectiveAction));
        }

        if (fromDate.HasValue)
        {
            var fromUtc = FormatHelper.VnDateToUtcStart(fromDate.Value.Date);
            query = query.Where(a => a.CreatedAt >= fromUtc);
        }

        if (toDate.HasValue)
        {
            var toUtc = FormatHelper.VnDateToUtcStart(toDate.Value.Date.AddDays(1)).AddTicks(-1);
            query = query.Where(a => a.CreatedAt <= toUtc);
        }

        var totalItems = await query.CountAsync();
        var logs = await query
            .OrderByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var distinctActions = await _db.AuditLogs
            .AsNoTracking()
            .Select(a => a.Action)
            .Distinct()
            .OrderBy(a => a)
            .ToListAsync();

        var qs = new List<string>();
        if (!string.IsNullOrWhiteSpace(userEmail)) qs.Add($"userEmail={Uri.EscapeDataString(userEmail.Trim())}");
        if (!string.IsNullOrWhiteSpace(effectiveAction)) qs.Add($"action={Uri.EscapeDataString(effectiveAction)}");
        if (fromDate.HasValue) qs.Add($"fromDate={fromDate.Value:yyyy-MM-dd}");
        if (toDate.HasValue) qs.Add($"toDate={toDate.Value:yyyy-MM-dd}");

        var baseQueryUrl = qs.Count > 0 ? $"/admin/audit-logs?{string.Join("&", qs)}" : "/admin/audit-logs";

        var vm = new AuditLogsAdminViewModel
        {
            UserEmail = userEmail,
            Action = effectiveAction,
            FromDate = fromDate,
            ToDate = toDate,
            DistinctActions = distinctActions,
            BaseQueryUrl = baseQueryUrl,
            PagedLogs = new PagedResult<AuditLog>
            {
                Items = logs,
                Page = page,
                PageSize = pageSize,
                TotalItems = totalItems
            }
        };

        return View("AuditLogs", vm);
    }
}
