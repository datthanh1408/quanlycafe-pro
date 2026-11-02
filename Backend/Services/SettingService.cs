using CoffeeShop.Data;
using CoffeeShop.Models.Entities;
using CoffeeShop.Models.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CoffeeShop.Services;

public interface ISettingService
{
    Task<StoreSettingsDto> GetStoreSettingsAsync();
    Task UpdateSettingsAsync(Dictionary<string, string> updates);
    Task<List<PaymentMethodConfig>> GetActivePaymentMethodsAsync();
    Task<List<PaymentMethodConfig>> GetAllPaymentMethodsAsync();
    Task TogglePaymentMethodAsync(string code, bool isActive);
    void InvalidateCache();
}

public class SettingService : ISettingService
{
    private const string CacheKey = "StoreSettings_Cache";
    private readonly AppDbContext _db;
    private readonly IMemoryCache _cache;

    public SettingService(AppDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<StoreSettingsDto> GetStoreSettingsAsync()
    {
        if (_cache.TryGetValue(CacheKey, out StoreSettingsDto? cached) && cached != null)
            return cached;

        var dict = await _db.SystemSettings
            .AsNoTracking()
            .ToDictionaryAsync(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);

        string Get(string k, string def) => dict.TryGetValue(k, out var v) && !string.IsNullOrWhiteSpace(v) ? v : def;
        string GetRaw(string k) => dict.TryGetValue(k, out var v) ? v : "";
        decimal GetDec(string k, decimal def) => dict.TryGetValue(k, out var v) && decimal.TryParse(v, out var d) ? d : def;
        int GetInt(string k, int def) => dict.TryGetValue(k, out var v) && int.TryParse(v, out var i) ? i : def;

        var dto = new StoreSettingsDto
        {
            ShopName = Get("ShopName", "Đạt Mộc Coffee"),
            Slogan = Get("Slogan", "Hương mộc nguyên bản – Trọn vị từng khoảnh khắc"),
            BusinessType = Get("BusinessType", "Cafe & Bakery"),
            Address = Get("Address", "128 Nguyễn Huệ, Phường Bến Nghé, Quận 1, TP. Hồ Chí Minh (Dữ liệu mẫu)"),
            Phone = Get("Phone", "0900 000 000"),
            Email = Get("Email", "contact@example.com"),
            OpeningHours = Get("OpeningHours", "07:00 – 22:00 hằng ngày"),
            FacebookUrl = GetRaw("FacebookUrl"),
            ZaloUrl = GetRaw("ZaloUrl"),
            GoogleMapsEmbedUrl = GetRaw("GoogleMapsEmbedUrl"),
            PrimaryColor = Get("PrimaryColor", "#6F4E37"),
            SecondaryColor = Get("SecondaryColor", "#F6EFE7"),
            AccentColor = Get("AccentColor", "#C8782A"),
            LogoUrl = GetRaw("LogoUrl"),
            BankName = GetRaw("BankName"),
            BankAccountNumber = GetRaw("BankAccountNumber"),
            BankAccountHolder = GetRaw("BankAccountHolder"),
            QrImageUrl = GetRaw("QrImageUrl"),
            ShippingFee = GetDec("ShippingFee", 15000m),
            FreeShippingThreshold = GetDec("FreeShippingThreshold", 150000m),
            LoyaltyEarnPerVnd = GetInt("LoyaltyEarnPerVnd", 10000),
            LoyaltyRedeemValueVnd = GetInt("LoyaltyRedeemValueVnd", 100),
            ReservationHoldMinutes = GetInt("ReservationHoldMinutes", 30)
        };

        _cache.Set(CacheKey, dto, TimeSpan.FromMinutes(10));
        return dto;
    }

    public async Task UpdateSettingsAsync(Dictionary<string, string> updates)
    {
        var keys = updates.Keys.ToList();
        var existing = await _db.SystemSettings
            .Where(s => keys.Contains(s.Key))
            .ToDictionaryAsync(s => s.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var kv in updates)
        {
            if (existing.TryGetValue(kv.Key, out var entity))
            {
                entity.Value = kv.Value ?? string.Empty;
                entity.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                _db.SystemSettings.Add(new SystemSetting
                {
                    Key = kv.Key,
                    Value = kv.Value ?? string.Empty,
                    UpdatedAt = DateTime.UtcNow
                });
            }
        }

        await _db.SaveChangesAsync();
        InvalidateCache();
    }

    public async Task<List<PaymentMethodConfig>> GetActivePaymentMethodsAsync()
    {
        var settings = await GetStoreSettingsAsync();
        var methods = await _db.PaymentMethods
            .AsNoTracking()
            .Where(m => m.IsActive)
            .OrderBy(m => m.DisplayOrder)
            .ToListAsync();

        // Ẩn phương thức chuyển khoản QR cho đến khi Admin cấu hình đầy đủ ngân hàng + ảnh QR (Mục 1)
        if (!settings.IsQrConfigured)
        {
            methods = methods.Where(m => !string.Equals(m.Code, "QR", StringComparison.OrdinalIgnoreCase)).ToList();
        }

        return methods;
    }

    public async Task<List<PaymentMethodConfig>> GetAllPaymentMethodsAsync()
    {
        return await _db.PaymentMethods
            .AsNoTracking()
            .OrderBy(m => m.DisplayOrder)
            .ToListAsync();
    }

    public async Task TogglePaymentMethodAsync(string code, bool isActive)
    {
        var method = await _db.PaymentMethods.FirstOrDefaultAsync(m => m.Code == code);
        if (method != null)
        {
            method.IsActive = isActive;
            await _db.SaveChangesAsync();
            InvalidateCache();
        }
    }

    public void InvalidateCache() => _cache.Remove(CacheKey);
}

