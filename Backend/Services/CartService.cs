using System.Text.Json;
using CoffeeShop.Models.Enums;
using CoffeeShop.Models.ViewModels;

namespace CoffeeShop.Services;

public interface ICartService
{
    List<CartItemSessionDto> GetRawItems();
    string? GetSavedPromoCode();
    void SetPromoCode(string? code);
    void AddItem(OrderRequestLineDto item);
    void UpdateQuantity(string lineId, int quantity);
    void RemoveItem(string lineId);
    void Clear();
    Task<CartSummaryDto> GetCartSummaryAsync(string? userId, OrderType orderType = OrderType.TakeAway);
}

public class CartService : ICartService
{
    private const string CartKey = "CoffeeShop_Cart_V1";
    private const string PromoKey = "CoffeeShop_Promo_V1";

    private readonly IHttpContextAccessor _http;
    private readonly IPricingService _pricingService;
    private readonly IPromotionService _promotionService;

    public CartService(IHttpContextAccessor http, IPricingService pricingService, IPromotionService promotionService)
    {
        _http = http;
        _pricingService = pricingService;
        _promotionService = promotionService;
    }

    private ISession? Session => _http.HttpContext?.Session;

    public List<CartItemSessionDto> GetRawItems()
    {
        var json = Session?.GetString(CartKey);
        if (string.IsNullOrWhiteSpace(json)) return new List<CartItemSessionDto>();
        try
        {
            return JsonSerializer.Deserialize<List<CartItemSessionDto>>(json) ?? new List<CartItemSessionDto>();
        }
        catch
        {
            return new List<CartItemSessionDto>();
        }
    }

    private void SaveRawItems(List<CartItemSessionDto> items)
    {
        Session?.SetString(CartKey, JsonSerializer.Serialize(items));
    }

    public string? GetSavedPromoCode() => Session?.GetString(PromoKey);

    public void SetPromoCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            Session?.Remove(PromoKey);
        else
            Session?.SetString(PromoKey, code.Trim().ToUpperInvariant());
    }

    public void AddItem(OrderRequestLineDto item)
    {
        var items = GetRawItems();
        var sortedToppings = (item.ToppingIds ?? new List<int>()).Distinct().OrderBy(x => x).ToList();
        var cleanNote = Helpers.FormatHelper.SanitizeNote(item.Note);

        // Gộp dòng nếu trùng hoàn toàn sản phẩm + tùy chọn
        var existing = items.FirstOrDefault(x =>
            x.ProductId == item.ProductId &&
            string.Equals(x.SizeName ?? "", item.SizeName ?? "", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.Temperature ?? "", item.Temperature ?? "", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.SugarLevel ?? "", item.SugarLevel ?? "", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.IceLevel ?? "", item.IceLevel ?? "", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.Note ?? "", cleanNote, StringComparison.Ordinal) &&
            x.ToppingIds.OrderBy(t => t).SequenceEqual(sortedToppings));

        if (existing != null)
        {
            existing.Quantity = Math.Clamp(existing.Quantity + Math.Max(1, item.Quantity), 1, 99);
        }
        else
        {
            items.Add(new CartItemSessionDto
            {
                LineId = Guid.NewGuid().ToString("N"),
                ProductId = item.ProductId,
                SizeName = item.SizeName,
                Temperature = item.Temperature,
                SugarLevel = item.SugarLevel,
                IceLevel = item.IceLevel,
                ToppingIds = sortedToppings,
                Quantity = Math.Clamp(item.Quantity, 1, 99),
                Note = cleanNote
            });
        }

        SaveRawItems(items);
    }

    public void UpdateQuantity(string lineId, int quantity)
    {
        var items = GetRawItems();
        var target = items.FirstOrDefault(x => x.LineId == lineId);
        if (target == null) return;

        if (quantity <= 0)
            items.Remove(target);
        else
            target.Quantity = Math.Clamp(quantity, 1, 99);

        SaveRawItems(items);
    }

    public void RemoveItem(string lineId)
    {
        var items = GetRawItems();
        items.RemoveAll(x => x.LineId == lineId);
        SaveRawItems(items);
    }

    public void Clear()
    {
        Session?.Remove(CartKey);
        Session?.Remove(PromoKey);
    }

    public async Task<CartSummaryDto> GetCartSummaryAsync(string? userId, OrderType orderType = OrderType.TakeAway)
    {
        var raw = GetRawItems();
        var reqPairs = raw.Select(r => (r.LineId, new OrderRequestLineDto
        {
            ProductId = r.ProductId,
            SizeName = r.SizeName,
            Temperature = r.Temperature,
            SugarLevel = r.SugarLevel,
            IceLevel = r.IceLevel,
            ToppingIds = r.ToppingIds,
            Quantity = r.Quantity,
            Note = r.Note
        }));

        var priced = await _pricingService.PriceLinesAsync(reqPairs);
        var subtotal = priced.Where(x => x.IsAvailable).Sum(x => x.LineTotal);

        var promoCode = GetSavedPromoCode();
        decimal discount = 0m;
        int? promoId = null;
        string? promoMsg = null;
        bool promoValid = false;

        if (!string.IsNullOrWhiteSpace(promoCode) && subtotal > 0)
        {
            var promoRes = await _promotionService.ValidateAndCalculateAsync(promoCode, subtotal, userId);
            promoValid = promoRes.IsValid;
            promoMsg = promoRes.Message;
            if (promoRes.IsValid)
            {
                discount = promoRes.DiscountAmount;
                promoId = promoRes.PromotionId;
            }
        }

        var shipping = subtotal > 0
            ? await _pricingService.CalculateShippingFeeAsync(orderType, subtotal - discount)
            : 0m;

        var total = _pricingService.CalculateFinalTotal(subtotal, discount, shipping);

        return new CartSummaryDto
        {
            Items = priced,
            Subtotal = subtotal,
            PromoCode = promoCode,
            PromotionId = promoId,
            PromoMessage = promoMsg,
            PromoValid = promoValid,
            DiscountAmount = discount,
            ShippingFee = shipping,
            Total = total
        };
    }
}

