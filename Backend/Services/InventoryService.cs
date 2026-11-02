using CoffeeShop.Data;
using CoffeeShop.Helpers;
using CoffeeShop.Models.Entities;
using CoffeeShop.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShop.Services;

public interface IInventoryService
{
    Task<(bool Sufficient, List<string> Shortages)> CheckStockForOrderAsync(Order order);
    Task<(bool Success, string? ErrorMessage)> DeductStockForBrewingInCurrentTxAsync(Order order, string actorName);
    Task<(bool Success, string? ErrorMessage)> RecordManualTransactionAsync(
        int ingredientId,
        StockTransactionType type,
        decimal quantity,
        decimal unitCost,
        string reason,
        string actorName);
    Task<List<Ingredient>> GetLowStockIngredientsAsync();
}

public class InventoryService : IInventoryService
{
    private readonly AppDbContext _db;
    private readonly IAuditService _auditService;

    public InventoryService(AppDbContext db, IAuditService auditService)
    {
        _db = db;
        _auditService = auditService;
    }

    private async Task<Dictionary<int, decimal>> ComputeRequiredIngredientsAsync(Order order)
    {
        var productIds = order.Items.Select(i => i.ProductId).Distinct().ToList();
        var recipes = await _db.Recipes
            .AsNoTracking()
            .Where(r => productIds.Contains(r.ProductId))
            .ToListAsync();

        var required = new Dictionary<int, decimal>();

        foreach (var item in order.Items)
        {
            // Lấy công thức áp dụng mọi size (SizeName == null/empty) + công thức riêng cho size đã chọn
            var matching = recipes.Where(r =>
                r.ProductId == item.ProductId &&
                (string.IsNullOrWhiteSpace(r.SizeName) ||
                 string.Equals(r.SizeName, item.SizeName, StringComparison.OrdinalIgnoreCase)));

            foreach (var r in matching)
            {
                var need = r.Quantity * item.Quantity;
                if (need <= 0) continue;
                required[r.IngredientId] = required.TryGetValue(r.IngredientId, out var cur) ? cur + need : need;
            }
        }

        return required;
    }

    public async Task<(bool Sufficient, List<string> Shortages)> CheckStockForOrderAsync(Order order)
    {
        var required = await ComputeRequiredIngredientsAsync(order);
        if (required.Count == 0) return (true, new List<string>());

        var ingIds = required.Keys.ToList();
        var ingredients = await _db.Ingredients
            .AsNoTracking()
            .Where(i => ingIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id);

        var shortages = new List<string>();
        foreach (var (ingId, needQty) in required)
        {
            if (!ingredients.TryGetValue(ingId, out var ing) || ing.IsDeleted || !ing.IsActive)
            {
                shortages.Add($"Nguyên liệu ID #{ingId} đã ngừng hoạt động.");
                continue;
            }

            if (ing.StockQty < needQty)
            {
                shortages.Add($"{ing.Name}: cần {needQty:0.##} {ing.Unit}, tồn kho chỉ còn {ing.StockQty:0.##} {ing.Unit}");
            }
        }

        return (shortages.Count == 0, shortages);
    }

    /// <summary>
    /// Thực hiện trừ kho theo công thức trong cùng transaction khi đơn chuyển sang "Đang pha chế" (Mục 6.6).
    /// </summary>
    public async Task<(bool Success, string? ErrorMessage)> DeductStockForBrewingInCurrentTxAsync(Order order, string actorName)
    {
        // Chống trừ kho lặp 2 lần cho cùng 1 đơn
        var alreadyDeducted = await _db.StockTransactions
            .AnyAsync(t => t.OrderId == order.Id && t.Type == StockTransactionType.Sale);
        if (alreadyDeducted) return (true, null);

        var required = await ComputeRequiredIngredientsAsync(order);
        if (required.Count == 0) return (true, null);

        var ingIds = required.Keys.ToList();
        var ingredients = await _db.Ingredients
            .Where(i => ingIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id);

        var shortages = new List<string>();
        foreach (var (ingId, needQty) in required)
        {
            if (!ingredients.TryGetValue(ingId, out var ing) || ing.IsDeleted || !ing.IsActive)
            {
                shortages.Add($"Nguyên liệu #{ingId} không khả dụng");
            }
            else if (ing.StockQty < needQty)
            {
                shortages.Add($"{ing.Name} (thiếu {(needQty - ing.StockQty):0.##} {ing.Unit})");
            }
        }

        if (shortages.Count > 0)
        {
            return (false, "Không đủ nguyên liệu pha chế: " + string.Join("; ", shortages));
        }

        foreach (var (ingId, needQty) in required)
        {
            var ing = ingredients[ingId];
            ing.StockQty -= needQty;

            _db.StockTransactions.Add(new StockTransaction
            {
                IngredientId = ing.Id,
                Type = StockTransactionType.Sale,
                Quantity = -needQty,
                BalanceAfter = ing.StockQty,
                UnitCost = 0,
                Reason = $"Trừ kho tự động theo đơn {order.Code}",
                OrderId = order.Id,
                CreatedBy = actorName,
                CreatedAt = order.UpdatedAt
            });
        }

        return (true, null);
    }

    public async Task<(bool Success, string? ErrorMessage)> RecordManualTransactionAsync(
        int ingredientId,
        StockTransactionType type,
        decimal quantity,
        decimal unitCost,
        string reason,
        string actorName)
    {
        var cleanReason = FormatHelper.SanitizeNote(reason);
        if (string.IsNullOrWhiteSpace(cleanReason))
            return (false, "Vui lòng nhập lý do giao dịch kho.");

        if (type != StockTransactionType.Adjust && quantity <= 0)
            return (false, "Số lượng phải lớn hơn 0.");

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var ing = await _db.Ingredients.FirstOrDefaultAsync(i => i.Id == ingredientId && !i.IsDeleted);
            if (ing == null)
                return (false, "Không tìm thấy nguyên liệu.");

            var oldStock = ing.StockQty;
            decimal signedDelta;

            switch (type)
            {
                case StockTransactionType.Import:
                    signedDelta = Math.Abs(quantity);
                    break;
                case StockTransactionType.Export:
                case StockTransactionType.Wastage:
                    signedDelta = -Math.Abs(quantity);
                    break;
                case StockTransactionType.Adjust:
                    // Với Điều chỉnh, quantity truyền vào là mức tồn thực tế mới (hoặc chênh lệch) -> ở UI ta truyền mức tồn thực tế mới nếu >= 0
                    if (quantity < 0)
                        return (false, "Tồn kho sau điều chỉnh không được âm.");
                    signedDelta = quantity - ing.StockQty;
                    break;
                default:
                    return (false, "Loại giao dịch thủ công không hợp lệ.");
            }

            var newBalance = ing.StockQty + signedDelta;
            if (newBalance < 0)
            {
                return (false, $"Tồn kho không được âm. Tồn hiện tại của {ing.Name} là {ing.StockQty:0.##} {ing.Unit}.");
            }

            ing.StockQty = newBalance;

            _db.StockTransactions.Add(new StockTransaction
            {
                IngredientId = ing.Id,
                Type = type,
                Quantity = signedDelta,
                BalanceAfter = newBalance,
                UnitCost = Math.Max(0m, unitCost),
                Reason = cleanReason,
                CreatedBy = actorName,
                CreatedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            await _auditService.LogAsync(
                action: $"Kho:{type}",
                entityName: "Ingredient",
                entityId: ing.Id.ToString(),
                oldValues: new { StockQty = oldStock },
                newValues: new { StockQty = newBalance, Reason = cleanReason });

            return (true, null);
        }
        catch (DbUpdateConcurrencyException)
        {
            await tx.RollbackAsync();
            return (false, "Dữ liệu tồn kho vừa được cập nhật bởi người khác. Vui lòng thử lại.");
        }
    }

    public async Task<List<Ingredient>> GetLowStockIngredientsAsync()
    {
        return await _db.Ingredients
            .AsNoTracking()
            .Where(i => !i.IsDeleted && i.IsActive && i.StockQty <= i.MinThreshold)
            .OrderBy(i => i.StockQty)
            .ToListAsync();
    }
}

