namespace CoffeeShop.Helpers;

public static class ImageUploadHelper
{
    private const long MaxFileSizeBytes = 2 * 1024 * 1024; // 2 MB
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp"
    };

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp"
    };

    public static async Task<(bool Success, string? RelativeUrl, string? ErrorMessage)> SaveImageAsync(
        IFormFile file,
        IWebHostEnvironment env,
        string subFolder = "products",
        string? oldRelativeUrl = null)
    {
        if (file == null || file.Length == 0)
            return (false, null, "Vui lòng chọn tệp hình ảnh.");

        if (file.Length > MaxFileSizeBytes)
            return (false, null, "Dung lượng ảnh vượt quá giới hạn tối đa 2 MB.");

        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(ext) || !AllowedExtensions.Contains(ext))
            return (false, null, "Chỉ chấp nhận định dạng ảnh JPG, PNG hoặc WEBP (không nhận SVG).");

        if (!AllowedContentTypes.Contains(file.ContentType))
            return (false, null, "Content-Type của tệp ảnh không hợp lệ.");

        // Kiểm tra magic bytes thực tế để chống giả mạo phần mở rộng
        await using var stream = file.OpenReadStream();
        var header = new byte[12];
        var read = await stream.ReadAsync(header.AsMemory(0, header.Length));
        if (!IsValidMagicBytes(header, read, ext))
            return (false, null, "Nội dung tệp không khớp chữ ký định dạng ảnh hợp lệ.");

        stream.Position = 0;

        var uploadDir = Path.Combine(env.WebRootPath, "uploads", subFolder);
        Directory.CreateDirectory(uploadDir);

        var fileName = $"{Guid.NewGuid():N}{ext.ToLowerInvariant()}";
        var fullPath = Path.Combine(uploadDir, fileName);

        await using (var fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write))
        {
            await stream.CopyToAsync(fs);
        }

        // Xóa ảnh upload cũ (không xóa ảnh seed mặc định)
        if (!string.IsNullOrWhiteSpace(oldRelativeUrl) &&
            oldRelativeUrl.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
        {
            var oldPath = Path.Combine(env.WebRootPath, oldRelativeUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(oldPath))
            {
                try { File.Delete(oldPath); } catch { /* ignore file lock */ }
            }
        }

        return (true, $"/uploads/{subFolder}/{fileName}", null);
    }

    private static bool IsValidMagicBytes(byte[] header, int length, string ext)
    {
        if (length < 4) return false;
        var lower = ext.ToLowerInvariant();

        if (lower is ".jpg" or ".jpeg")
        {
            return header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
        }

        if (lower == ".png")
        {
            return length >= 8 &&
                   header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&
                   header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A;
        }

        if (lower == ".webp")
        {
            return length >= 12 &&
                   header[0] == (byte)'R' && header[1] == (byte)'I' && header[2] == (byte)'F' && header[3] == (byte)'F' &&
                   header[8] == (byte)'W' && header[9] == (byte)'E' && header[10] == (byte)'B' && header[11] == (byte)'P';
        }

        return false;
    }
}

