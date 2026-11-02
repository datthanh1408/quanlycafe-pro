using System.Text.RegularExpressions;
using CoffeeShop.Areas.Admin.Models;
using CoffeeShop.Authorization;
using CoffeeShop.Data;
using CoffeeShop.Helpers;
using CoffeeShop.Models.Entities;
using CoffeeShop.Models.Enums;
using CoffeeShop.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShop.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = AppRoles.Admin)]
public class PromotionsAdminController : Controller
{
    private readonly AppDbContext _db;
    private readonly IAuditService _auditService;

    public PromotionsAdminController(AppDbContext db, IAuditService auditService)
    {
        _db = db;
        _auditService = auditService;
    }

    [HttpGet("/admin/promotions")]
    public async Task<IActionResult> Index(string? search = null, int? editId = null)
    {
        var query = _db.Promotions
            .AsNoTracking()
            .Where(p => !p.IsDeleted);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var kw = search.Trim();
            query = query.Where(p => p.Code.Contains(kw) || p.Name.Contains(kw));
        }

        var list = await query
            .OrderByDescending(p => p.IsActive)
            .ThenByDescending(p => p.EndAt)
            .ToListAsync();

        var vnNow = FormatHelper.VnNow();
        var form = new PromotionFormModel
        {
            Type = PromotionType.Percentage,
            Value = 15,
            MaxDiscount = 30000,
            MinOrderValue = 50000,
            StartAt = new DateTime(vnNow.Year, vnNow.Month, vnNow.Day, 0, 0, 0),
            EndAt = new DateTime(vnNow.Year, vnNow.Month, vnNow.Day, 23, 59, 0).AddDays(30),
            UsageLimit = 500,
            PerUserLimit = 3,
            IsActive = true
        };

        if (editId.HasValue)
        {
            var target = await _db.Promotions.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == editId.Value && !p.IsDeleted);
            if (target != null)
            {
                form = new PromotionFormModel
                {
                    Id = target.Id,
                    Code = target.Code,
                    Name = target.Name,
                    Type = target.Type,
                    Value = target.Value,
                    MaxDiscount = target.MaxDiscount,
                    MinOrderValue = target.MinOrderValue,
                    StartAt = FormatHelper.ToVnTime(target.StartAt),
                    EndAt = FormatHelper.ToVnTime(target.EndAt),
                    UsageLimit = target.UsageLimit,
                    PerUserLimit = target.PerUserLimit,
                    IsActive = target.IsActive
                };
            }
        }

        return View(new PromotionsAdminViewModel
        {
            Search = search,
            Promotions = list,
            Form = form
        });
    }

    [HttpPost("/admin/promotions/save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(PromotionFormModel form)
    {
        if (string.IsNullOrWhiteSpace(form.Code) || string.IsNullOrWhiteSpace(form.Name))
        {
            TempData["ToastError"] = "Vui lòng nhập đầy đủ Mã khuyến mãi và Tên chương trình.";
            return RedirectToAction(nameof(Index), new { editId = form.Id > 0 ? (int?)form.Id : null });
        }

        var cleanCode = form.Code.Trim().ToUpperInvariant();
        if (!Regex.IsMatch(cleanCode, @"^[A-Z0-9\-]+$"))
        {
            TempData["ToastError"] = "Mã khuyến mãi chỉ được phép chứa chữ cái không dấu (A-Z), chữ số (0-9) và dấu gạch ngang (-).";
            return RedirectToAction(nameof(Index), new { editId = form.Id > 0 ? (int?)form.Id : null });
        }

        if (form.Type == PromotionType.Percentage && (form.Value < 1 || form.Value > 100))
        {
            TempData["ToastError"] = "Mức giảm theo phần trăm (%) phải nằm trong khoảng từ 1% đến 100%.";
            return RedirectToAction(nameof(Index), new { editId = form.Id > 0 ? (int?)form.Id : null });
        }

        if (form.Type == PromotionType.FixedAmount && form.Value < 1000)
        {
            TempData["ToastError"] = "Mức giảm số tiền cố định phải từ 1.000 ₫ trở lên.";
            return RedirectToAction(nameof(Index), new { editId = form.Id > 0 ? (int?)form.Id : null });
        }

        var startUtc = FormatHelper.VnDateToUtcStart(form.StartAt.Date).Add(form.StartAt.TimeOfDay);
        var endUtc = FormatHelper.VnDateToUtcStart(form.EndAt.Date).Add(form.EndAt.TimeOfDay);

        if (endUtc <= startUtc)
        {
            TempData["ToastError"] = "Thời điểm kết thúc bắt buộc phải lớn hơn thời điểm bắt đầu.";
            return RedirectToAction(nameof(Index), new { editId = form.Id > 0 ? (int?)form.Id : null });
        }

        decimal? maxDiscount = form.Type == PromotionType.Percentage && form.MaxDiscount.HasValue && form.MaxDiscount.Value > 0
            ? Math.Round(form.MaxDiscount.Value, 0)
            : null;

        var minOrder = Math.Max(0m, Math.Round(form.MinOrderValue, 0));
        var usageLimit = Math.Max(1, form.UsageLimit);
        var perUserLimit = Math.Max(1, form.PerUserLimit);

        if (form.Id == 0)
        {
            var existing = await _db.Promotions.FirstOrDefaultAsync(p => p.Code == cleanCode);
            if (existing != null && !existing.IsDeleted)
            {
                TempData["ToastError"] = $"Mã khuyến mãi '{cleanCode}' đã tồn tại.";
                return RedirectToAction(nameof(Index));
            }

            if (existing != null && existing.IsDeleted)
            {
                existing.IsDeleted = false;
                existing.Name = form.Name.Trim();
                existing.Type = form.Type;
                existing.Value = Math.Round(form.Value, 0);
                existing.MaxDiscount = maxDiscount;
                existing.MinOrderValue = minOrder;
                existing.StartAt = startUtc;
                existing.EndAt = endUtc;
                existing.UsageLimit = usageLimit;
                existing.PerUserLimit = perUserLimit;
                existing.IsActive = form.IsActive;

                await _db.SaveChangesAsync();
                await _auditService.LogAsync("RestorePromotion", "Promotion", existing.Id.ToString(), null,
                    new { existing.Code, existing.Name, existing.Type, existing.Value });

                TempData["ToastSuccess"] = $"Đã kích hoạt lại và cập nhật mã khuyến mãi '{existing.Code}'.";
                return RedirectToAction(nameof(Index));
            }

            var promo = new Promotion
            {
                Code = cleanCode,
                Name = form.Name.Trim(),
                Type = form.Type,
                Value = Math.Round(form.Value, 0),
                MaxDiscount = maxDiscount,
                MinOrderValue = minOrder,
                StartAt = startUtc,
                EndAt = endUtc,
                UsageLimit = usageLimit,
                PerUserLimit = perUserLimit,
                UsedCount = 0,
                IsActive = form.IsActive,
                IsDeleted = false
            };

            _db.Promotions.Add(promo);
            await _db.SaveChangesAsync();

            await _auditService.LogAsync("CreatePromotion", "Promotion", promo.Id.ToString(), null,
                new { promo.Code, promo.Name, Type = promo.Type.ToString(), promo.Value, promo.EndAt });

            TempData["ToastSuccess"] = $"Đã tạo chương trình khuyến mãi '{promo.Code}'.";
        }
        else
        {
            var promo = await _db.Promotions.FirstOrDefaultAsync(p => p.Id == form.Id && !p.IsDeleted);
            if (promo == null)
            {
                TempData["ToastError"] = "Không tìm thấy chương trình khuyến mãi.";
                return RedirectToAction(nameof(Index));
            }

            var duplicate = await _db.Promotions.AnyAsync(p => p.Code == cleanCode && p.Id != promo.Id);
            if (duplicate)
            {
                TempData["ToastError"] = $"Mã '{cleanCode}' đã được dùng cho chương trình khác.";
                return RedirectToAction(nameof(Index), new { editId = promo.Id });
            }

            var oldSnap = new { promo.Code, promo.Name, Type = promo.Type.ToString(), promo.Value, promo.EndAt, promo.IsActive };

            promo.Code = cleanCode;
            promo.Name = form.Name.Trim();
            promo.Type = form.Type;
            promo.Value = Math.Round(form.Value, 0);
            promo.MaxDiscount = maxDiscount;
            promo.MinOrderValue = minOrder;
            promo.StartAt = startUtc;
            promo.EndAt = endUtc;
            promo.UsageLimit = usageLimit;
            promo.PerUserLimit = perUserLimit;
            promo.IsActive = form.IsActive;

            await _db.SaveChangesAsync();

            await _auditService.LogAsync("UpdatePromotion", "Promotion", promo.Id.ToString(), oldSnap,
                new { promo.Code, promo.Name, Type = promo.Type.ToString(), promo.Value, promo.EndAt, promo.IsActive });

            TempData["ToastSuccess"] = $"Đã cập nhật chương trình khuyến mãi '{promo.Code}'.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("/admin/promotions/toggle/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int id)
    {
        var promo = await _db.Promotions.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
        if (promo == null)
        {
            TempData["ToastError"] = "Không tìm thấy mã khuyến mãi.";
            return RedirectToAction(nameof(Index));
        }

        promo.IsActive = !promo.IsActive;
        await _db.SaveChangesAsync();

        await _auditService.LogAsync("TogglePromotion", "Promotion", promo.Id.ToString(),
            new { IsActive = !promo.IsActive }, new { promo.IsActive });

        TempData["ToastSuccess"] = $"Mã '{promo.Code}' đã chuyển sang {(promo.IsActive ? "Hoạt động" : "Tạm ngưng")}.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("/admin/promotions/delete/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var promo = await _db.Promotions.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
        if (promo == null)
        {
            TempData["ToastError"] = "Không tìm thấy mã khuyến mãi.";
            return RedirectToAction(nameof(Index));
        }

        promo.IsDeleted = true;
        promo.IsActive = false;
        await _db.SaveChangesAsync();

        await _auditService.LogAsync("DeletePromotion", "Promotion", promo.Id.ToString(),
            new { promo.Code, promo.Name }, new { IsDeleted = true });

        TempData["ToastSuccess"] = $"Đã xóa mã khuyến mãi '{promo.Code}'.";
        return RedirectToAction(nameof(Index));
    }
}
