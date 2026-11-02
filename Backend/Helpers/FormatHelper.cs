using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace CoffeeShop.Helpers;

public static class FormatHelper
{
    private static readonly CultureInfo ViCulture = new("vi-VN");
    private static readonly TimeZoneInfo VnTimeZone = ResolveVnTimeZone();

    private static TimeZoneInfo ResolveVnTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        }
        catch
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
            }
            catch
            {
                return TimeZoneInfo.CreateCustomTimeZone("VN", TimeSpan.FromHours(7), "Vietnam Time", "Vietnam Time");
            }
        }
    }

    public static string Vnd(decimal amount)
    {
        var rounded = Math.Round(amount, 0, MidpointRounding.AwayFromZero);
        return string.Format(ViCulture, "{0:N0} ₫", rounded).Replace(",", ".");
    }

    public static DateTime ToVnTime(DateTime utcTime)
    {
        var utc = utcTime.Kind == DateTimeKind.Utc
            ? utcTime
            : DateTime.SpecifyKind(utcTime, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(utc, VnTimeZone);
    }

    public static DateTime VnNow() => ToVnTime(DateTime.UtcNow);

    public static DateTime VnDateToUtcStart(DateTime vnDate)
    {
        var unspecified = new DateTime(vnDate.Year, vnDate.Month, vnDate.Day, 0, 0, 0, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(unspecified, VnTimeZone);
    }

    public static string DateTimeVn(DateTime utcTime, string format = "dd/MM/yyyy HH:mm")
    {
        return ToVnTime(utcTime).ToString(format, ViCulture);
    }

    public static string VnDateTime(DateTime utcTime) => DateTimeVn(utcTime, "dd/MM/yyyy HH:mm");

    public static string VnDate(DateTime utcTime) => DateTimeVn(utcTime, "dd/MM/yyyy");

    public static string Slugify(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "item";
        var normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in normalized)
        {
            var uc = CharUnicodeInfo.GetUnicodeCategory(c);
            if (uc != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c switch
                {
                    'đ' => 'd',
                    'Đ' => 'd',
                    _ => char.ToLowerInvariant(c)
                });
            }
        }
        var clean = Regex.Replace(sb.ToString().Normalize(NormalizationForm.FormC), @"[^a-z0-9\s-]", "");
        clean = Regex.Replace(clean, @"[\s-]+", "-").Trim('-');
        return string.IsNullOrEmpty(clean) ? "item" : clean;
    }

    public static string SanitizeNote(string? input, int maxLen = 200)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var trimmed = input.Trim();
        // Loại bỏ ký tự điều khiển
        trimmed = Regex.Replace(trimmed, @"[\x00-\x08\x0B\x0C\x0E-\x1F]", "");
        return trimmed.Length <= maxLen ? trimmed : trimmed[..maxLen];
    }
}

