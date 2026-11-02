using CoffeeShop.Data;
using CoffeeShop.Helpers;
using CoffeeShop.Models.Enums;
using CoffeeShop.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShop.Services;

public interface IPromotionService
{
    Task<PromotionValidationResult> ValidateAndCalculateAsync(string? code, decimal subtotal, string? userId);
}

public class PromotionService : IPromotionService
{
    private readonly AppDbContext _db;

    public PromotionService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<PromotionValidationResult> ValidateAndCalculateAsync(string? code, decimal subtotal, string? userId)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return new PromotionValidationResult { IsValid = false, Message = string.Empty };
        }

        var normalized = code.Trim().ToUpperInvariant();
        var promo = await _db.Promotions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Code == normalized && !p.IsDeleted);

        if (promo == null)
        {
            return new PromotionValidationResult
            {
                IsValid = false,
                Message = $"Mã giảm giá '{normalized}' không tồn tại."
            };
        }

        if (!promo.IsActive)
        {
            return new PromotionValidationResult
            {
                IsValid = false,
                Message = $"Mã giảm giá '{normalized}' hiện đang tạm ngưng áp dụng."
            };
        }

        var now = DateTime.UtcNow;
        if (now < promo.StartAt)
        {
            return new PromotionValidationResult
            {
                IsValid = false,
                Message = $"Mã '{normalized}' chưa đến thời gian bắt đầu ({FormatHelper.DateTimeVn(promo.StartAt)})."
            };
        }

        if (now > promo.EndAt)
        {
            return new PromotionValidationResult
            {
                IsValid = false,
                Message = $"Mã giảm giá '{normalized}' đã hết hạn vào {FormatHelper.DateTimeVn(promo.EndAt)}."
            };
        }

        if (promo.UsageLimit > 0 && promo.UsedCount >= promo.UsageLimit)
        {
            return new PromotionValidationResult
            {
                IsValid = false,
                Message = $"Mã giảm giá '{normalized}' đã hết lượt sử dụng."
            };
        }

        if (subtotal < promo.MinOrderValue)
        {
            return new PromotionValidationResult
            {
                IsValid = false,
                Message = $"Đơn hàng cần tối thiểu {FormatHelper.Vnd(promo.MinOrderValue)} để dùng mã '{normalized}'."
            };
        }

        if (!string.IsNullOrWhiteSpace(userId) && promo.PerUserLimit > 0)
        {
            var userUsed = await _db.PromotionUsages
                .CountAsync(u => u.PromotionId == promo.Id && u.UserId == userId);

            if (userUsed >= promo.PerUserLimit)
            {
                return new PromotionValidationResult
                {
                    IsValid = false,
                    Message = $"Bạn đã sử dụng hết số lượt tối đa ({promo.PerUserLimit} lần) cho mã '{normalized}'."
                };
            }
        }

        decimal discount = promo.Type == PromotionType.Percentage
            ? Math.Round(subtotal * promo.Value / 100m, 0, MidpointRounding.AwayFromZero)
            : promo.Value;

        if (promo.Type == PromotionType.Percentage && promo.MaxDiscount.HasValue && promo.MaxDiscount.Value > 0)
        {
            discount = Math.Min(discount, promo.MaxDiscount.Value);
        }

        // Không vượt quá tạm tính và không áp lên phí giao hàng (Mục 6.5)
        discount = Math.Clamp(discount, 0m, subtotal);

        return new PromotionValidationResult
        {
            IsValid = true,
            PromotionId = promo.Id,
            Code = promo.Code,
            DiscountAmount = discount,
            Message = $"Đã áp dụng mã {promo.Code}: giảm {FormatHelper.Vnd(discount)}."
        };
    }
}

