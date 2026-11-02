using CoffeeShop.Data;
using CoffeeShop.Helpers;
using CoffeeShop.Models.Enums;
using CoffeeShop.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShop.Services;

public interface IPricingService
{
    Task<List<PricedLineItemDto>> PriceLinesAsync(IEnumerable<(string LineId, OrderRequestLineDto Line)> requests);
    Task<decimal> CalculateShippingFeeAsync(OrderType orderType, decimal subtotalAfterDiscount);
    decimal CalculateFinalTotal(decimal subtotal, decimal discountAmount, decimal shippingFee);
}

public class PricingService : IPricingService
{
    private readonly AppDbContext _db;
    private readonly ISettingService _settingService;

    public PricingService(AppDbContext db, ISettingService settingService)
    {
        _db = db;
        _settingService = settingService;
    }

    public async Task<List<PricedLineItemDto>> PriceLinesAsync(IEnumerable<(string LineId, OrderRequestLineDto Line)> requests)
    {
        var reqList = requests.ToList();
        if (reqList.Count == 0) return new List<PricedLineItemDto>();

        var productIds = reqList.Select(r => r.Line.ProductId).Distinct().ToList();
        var products = await _db.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Sizes)
            .Include(p => p.ProductToppings)
                .ThenInclude(pt => pt.Topping)
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        var result = new List<PricedLineItemDto>();

        foreach (var (lineId, req) in reqList)
        {
            var qty = Math.Clamp(req.Quantity, 1, 99);

            if (!products.TryGetValue(req.ProductId, out var product) || product.IsDeleted)
            {
                result.Add(new PricedLineItemDto
                {
                    LineId = lineId,
                    ProductId = req.ProductId,
                    ProductName = "Món không còn tồn tại",
                    ImageUrl = "/images/seed/default-drink.svg",
                    IsAvailable = false,
                    UnavailableReason = "Món này đã ngừng kinh doanh.",
                    Quantity = qty
                });
                continue;
            }

            var available = product.IsAvailable && product.Category.IsActive && !product.Category.IsDeleted;
            string? unavailableReason = available ? null : "Món này hiện đang tạm hết hàng.";

            // Xác định giá theo Size
            decimal sizePrice = product.BasePrice;
            string? resolvedSize = null;
            var activeSizes = product.Sizes.OrderBy(s => s.DisplayOrder).ToList();

            if (activeSizes.Count > 0)
            {
                var chosen = !string.IsNullOrWhiteSpace(req.SizeName)
                    ? activeSizes.FirstOrDefault(s => string.Equals(s.SizeName, req.SizeName, StringComparison.OrdinalIgnoreCase))
                    : null;
                chosen ??= activeSizes.First();
                resolvedSize = chosen.SizeName;
                sizePrice = chosen.Price;
            }

            // Chỉ chấp nhận các topping hợp lệ được gắn với sản phẩm và đang Active
            var requestedToppingIds = (req.ToppingIds ?? new List<int>()).Distinct().ToHashSet();
            var validToppings = product.ProductToppings
                .Select(pt => pt.Topping)
                .Where(t => !t.IsDeleted && t.IsActive && requestedToppingIds.Contains(t.Id))
                .OrderBy(t => t.Id)
                .Select(t => (t.Id, t.Name, t.Price))
                .ToList();

            var toppingSum = validToppings.Sum(t => t.Price);
            var unitPrice = sizePrice + toppingSum;
            var lineTotal = unitPrice * qty;

            result.Add(new PricedLineItemDto
            {
                LineId = lineId,
                ProductId = product.Id,
                ProductName = product.Name,
                ImageUrl = product.ImageUrl,
                IsAvailable = available,
                UnavailableReason = unavailableReason,
                SizeName = resolvedSize,
                SizePrice = sizePrice,
                Temperature = product.AllowTemperature ? (string.IsNullOrWhiteSpace(req.Temperature) ? "Lạnh" : req.Temperature) : null,
                SugarLevel = product.AllowSugar ? (string.IsNullOrWhiteSpace(req.SugarLevel) ? "100%" : req.SugarLevel) : null,
                IceLevel = (product.AllowIce && !string.Equals(req.Temperature, "Nóng", StringComparison.OrdinalIgnoreCase))
                    ? (string.IsNullOrWhiteSpace(req.IceLevel) ? "100%" : req.IceLevel)
                    : null,
                Toppings = validToppings,
                UnitPrice = unitPrice,
                Quantity = qty,
                LineTotal = lineTotal,
                Note = FormatHelper.SanitizeNote(req.Note)
            });
        }

        return result;
    }

    public async Task<decimal> CalculateShippingFeeAsync(OrderType orderType, decimal subtotalAfterDiscount)
    {
        if (orderType != OrderType.Delivery) return 0m;
        var settings = await _settingService.GetStoreSettingsAsync();
        if (subtotalAfterDiscount >= settings.FreeShippingThreshold) return 0m;
        return settings.ShippingFee;
    }

    public decimal CalculateFinalTotal(decimal subtotal, decimal discountAmount, decimal shippingFee)
    {
        var clampedDiscount = Math.Clamp(discountAmount, 0m, subtotal);
        return Math.Max(0m, subtotal - clampedDiscount + Math.Max(0m, shippingFee));
    }
}

