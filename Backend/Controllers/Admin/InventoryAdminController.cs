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
public class InventoryAdminController : Controller
{
    private readonly AppDbContext _db;
    private readonly IInventoryService _inventoryService;
    private readonly IAuditService _auditService;
    private readonly UserManager<ApplicationUser> _userManager;

    public InventoryAdminController(
        AppDbContext db,
        IInventoryService inventoryService,
        IAuditService auditService,
        UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _inventoryService = inventoryService;
        _auditService = auditService;
        _userManager = userManager;
    }

    [HttpGet("/admin/inventory")]
    public async Task<IActionResult> Index(
        string? search = null,
        bool lowStockOnly = false,
        int? ingredientId = null,
        StockTransactionType? type = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int page = 1)
    {
        const int pageSize = 15;
        page = Math.Max(1, page);

        var ingQuery = _db.Ingredients
            .AsNoTracking()
            .Where(i => !i.IsDeleted);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var kw = search.Trim();
            ingQuery = ingQuery.Where(i => i.Code.Contains(kw) || i.Name.Contains(kw));
        }

        if (lowStockOnly)
        {
            ingQuery = ingQuery.Where(i => i.StockQty <= i.MinThreshold);
        }

        var ingredients = await ingQuery
            .OrderByDescending(i => i.StockQty <= i.MinThreshold)
            .ThenBy(i => i.Code)
            .ToListAsync();

        var allDropdown = await _db.Ingredients
            .AsNoTracking()
            .Where(i => !i.IsDeleted)
            .OrderBy(i => i.Name)
            .ToListAsync();

        // Lịch sử giao dịch kho có phân trang
        var txQuery = _db.StockTransactions
            .AsNoTracking()
            .Include(t => t.Ingredient)
            .Include(t => t.Order)
            .AsQueryable();

        if (ingredientId.HasValue && ingredientId.Value > 0)
            txQuery = txQuery.Where(t => t.IngredientId == ingredientId.Value);

        if (type.HasValue)
            txQuery = txQuery.Where(t => t.Type == type.Value);

        if (fromDate.HasValue)
        {
            var fromUtc = FormatHelper.VnDateToUtcStart(fromDate.Value.Date);
            txQuery = txQuery.Where(t => t.CreatedAt >= fromUtc);
        }

        if (toDate.HasValue)
        {
            var toUtc = FormatHelper.VnDateToUtcStart(toDate.Value.Date.AddDays(1)).AddTicks(-1);
            txQuery = txQuery.Where(t => t.CreatedAt <= toUtc);
        }

        var totalTx = await txQuery.CountAsync();
        var txItems = await txQuery
            .OrderByDescending(t => t.CreatedAt)
            .ThenByDescending(t => t.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var qs = new List<string>();
        if (!string.IsNullOrWhiteSpace(search)) qs.Add($"search={Uri.EscapeDataString(search.Trim())}");
        if (lowStockOnly) qs.Add("lowStockOnly=true");
        if (ingredientId.HasValue) qs.Add($"ingredientId={ingredientId.Value}");
        if (type.HasValue) qs.Add($"type={type.Value}");
        if (fromDate.HasValue) qs.Add($"fromDate={fromDate.Value:yyyy-MM-dd}");
        if (toDate.HasValue) qs.Add($"toDate={toDate.Value:yyyy-MM-dd}");

        var baseQueryUrl = qs.Count > 0 ? $"/admin/inventory?{string.Join("&", qs)}" : "/admin/inventory";

        var vm = new InventoryAdminViewModel
        {
            Search = search,
            LowStockOnly = lowStockOnly,
            Ingredients = ingredients,
            AllIngredientsDropdown = allDropdown,
            TxIngredientId = ingredientId,
            TxType = type,
            FromDate = fromDate,
            ToDate = toDate,
            BaseQueryUrl = baseQueryUrl,
            PagedTransactions = new PagedResult<StockTransaction>
            {
                Items = txItems,
                Page = page,
                PageSize = pageSize,
                TotalItems = totalTx
            }
        };

        return View(vm);
    }

    [HttpPost("/admin/inventory/save-ingredient")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveIngredient(IngredientFormModel form)
    {
        if (string.IsNullOrWhiteSpace(form.Code) || string.IsNullOrWhiteSpace(form.Name) || string.IsNullOrWhiteSpace(form.Unit))
        {
            TempData["ToastError"] = "Vui lòng điền đầy đủ Mã, Tên nguyên liệu và Đơn vị tính.";
            return RedirectToAction(nameof(Index));
        }

        var cleanCode = form.Code.Trim().ToUpperInvariant();
        var cleanName = form.Name.Trim();
        var cleanUnit = form.Unit.Trim();
        var threshold = Math.Max(0m, Math.Round(form.MinThreshold, 2));

        if (form.Id == 0)
        {
            var existing = await _db.Ingredients.FirstOrDefaultAsync(i => i.Code == cleanCode);
            if (existing != null && !existing.IsDeleted)
            {
                TempData["ToastError"] = $"Mã nguyên liệu '{cleanCode}' đã tồn tại.";
                return RedirectToAction(nameof(Index));
            }

            if (existing != null && existing.IsDeleted)
            {
                existing.IsDeleted = false;
                existing.Name = cleanName;
                existing.Unit = cleanUnit;
                existing.MinThreshold = threshold;
                existing.IsActive = form.IsActive;
                await _db.SaveChangesAsync();

                await _auditService.LogAsync("RestoreIngredient", "Ingredient", existing.Id.ToString(), null,
                    new { existing.Code, existing.Name, existing.Unit, existing.MinThreshold });

                TempData["ToastSuccess"] = $"Đã thêm nguyên liệu '{existing.Name}' ({existing.Code}).";
                return RedirectToAction(nameof(Index));
            }

            var ing = new Ingredient
            {
                Code = cleanCode,
                Name = cleanName,
                Unit = cleanUnit,
                StockQty = 0m,
                MinThreshold = threshold,
                IsActive = form.IsActive,
                IsDeleted = false
            };

            _db.Ingredients.Add(ing);
            await _db.SaveChangesAsync();

            await _auditService.LogAsync("CreateIngredient", "Ingredient", ing.Id.ToString(), null,
                new { ing.Code, ing.Name, ing.Unit, ing.MinThreshold, ing.IsActive });

            TempData["ToastSuccess"] = $"Đã tạo nguyên liệu mới '{ing.Name}' ({ing.Code}). Hãy thực hiện Nhập kho để cập nhật tồn đầu kỳ.";
        }
        else
        {
            var ing = await _db.Ingredients.FirstOrDefaultAsync(i => i.Id == form.Id && !i.IsDeleted);
            if (ing == null)
            {
                TempData["ToastError"] = "Không tìm thấy nguyên liệu.";
                return RedirectToAction(nameof(Index));
            }

            var codeTaken = await _db.Ingredients.AnyAsync(i => i.Code == cleanCode && i.Id != ing.Id);
            if (codeTaken)
            {
                TempData["ToastError"] = $"Mã nguyên liệu '{cleanCode}' đã được dùng cho nguyên liệu khác.";
                return RedirectToAction(nameof(Index));
            }

            var oldSnap = new { ing.Code, ing.Name, ing.Unit, ing.MinThreshold, ing.IsActive };
            ing.Code = cleanCode;
            ing.Name = cleanName;
            ing.Unit = cleanUnit;
            ing.MinThreshold = threshold;
            ing.IsActive = form.IsActive;

            await _db.SaveChangesAsync();

            await _auditService.LogAsync("UpdateIngredient", "Ingredient", ing.Id.ToString(), oldSnap,
                new { ing.Code, ing.Name, ing.Unit, ing.MinThreshold, ing.IsActive });

            TempData["ToastSuccess"] = $"Đã lưu thông tin nguyên liệu '{ing.Name}'.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("/admin/inventory/delete-ingredient/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteIngredient(int id)
    {
        var ing = await _db.Ingredients.FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted);
        if (ing == null)
        {
            TempData["ToastError"] = "Không tìm thấy nguyên liệu.";
            return RedirectToAction(nameof(Index));
        }

        var usedInActiveProduct = await _db.Recipes
            .Include(r => r.Product)
            .AnyAsync(r => r.IngredientId == id && !r.Product.IsDeleted);

        if (usedInActiveProduct)
        {
            TempData["ToastError"] = $"Không thể xóa nguyên liệu '{ing.Name}' vì đang được gán trong công thức định lượng của sản phẩm. Hãy gỡ khỏi công thức trước hoặc chuyển sang Tạm ngưng.";
            return RedirectToAction(nameof(Index));
        }

        ing.IsDeleted = true;
        ing.IsActive = false;
        await _db.SaveChangesAsync();

        await _auditService.LogAsync("DeleteIngredient", "Ingredient", ing.Id.ToString(),
            new { ing.Code, ing.Name }, new { IsDeleted = true });

        TempData["ToastSuccess"] = $"Đã xóa nguyên liệu '{ing.Name}'.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("/admin/inventory/transaction")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RecordTransaction(StockTransactionFormModel form)
    {
        if (form.IngredientId <= 0)
        {
            TempData["ToastError"] = "Vui lòng chọn nguyên liệu thực hiện giao dịch.";
            return RedirectToAction(nameof(Index));
        }

        if (form.Type == StockTransactionType.Sale)
        {
            TempData["ToastError"] = "Giao dịch Bán hàng chỉ do hệ thống tự động trừ theo công thức khi pha chế.";
            return RedirectToAction(nameof(Index));
        }

        var user = await _userManager.GetUserAsync(User);
        var actorName = !string.IsNullOrWhiteSpace(user?.FullName)
            ? $"{user.FullName} (Admin)"
            : (User.Identity?.Name ?? "Admin");

        var (ok, err) = await _inventoryService.RecordManualTransactionAsync(
            ingredientId: form.IngredientId,
            type: form.Type,
            quantity: form.Quantity,
            unitCost: Math.Max(0m, form.UnitCost),
            reason: form.Reason,
            actorName: actorName);

        if (!ok)
        {
            TempData["ToastError"] = err ?? "Không thể ghi nhận giao dịch kho.";
        }
        else
        {
            TempData["ToastSuccess"] = $"Đã ghi nhận giao dịch '{form.Type.GetDisplayName()}' thành công.";
        }

        return RedirectToAction(nameof(Index));
    }
}
