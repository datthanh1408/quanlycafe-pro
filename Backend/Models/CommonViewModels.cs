using System.ComponentModel.DataAnnotations;
using CoffeeShop.Models.Enums;

namespace CoffeeShop.Models.ViewModels;

public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalItems { get; set; }
    public int TotalPages => PageSize <= 0 ? 1 : Math.Max(1, (int)Math.Ceiling((double)TotalItems / PageSize));
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
}

public class StoreSettingsDto
{
    public string ShopName { get; set; } = "Đạt Mộc Coffee";
    public string Slogan { get; set; } = "Hương mộc nguyên bản – Trọn vị từng khoảnh khắc";
    public string BusinessType { get; set; } = "Cafe & Bakery";
    public string Address { get; set; } = "128 Nguyễn Huệ, Phường Bến Nghé, Quận 1, TP. Hồ Chí Minh (Dữ liệu mẫu)";
    public string Phone { get; set; } = "0900 000 000";
    public string Email { get; set; } = "contact@example.com";
    public string OpeningHours { get; set; } = "07:00 – 22:00 hằng ngày";
    public string FacebookUrl { get; set; } = "";
    public string ZaloUrl { get; set; } = "";
    public string GoogleMapsEmbedUrl { get; set; } = "";
    public string PrimaryColor { get; set; } = "#6F4E37";
    public string SecondaryColor { get; set; } = "#F6EFE7";
    public string AccentColor { get; set; } = "#C8782A";
    public string LogoUrl { get; set; } = "";
    public string BankName { get; set; } = "";
    public string BankAccountNumber { get; set; } = "";
    public string BankAccountHolder { get; set; } = "";
    public string QrImageUrl { get; set; } = "";
    public decimal ShippingFee { get; set; } = 15000;
    public decimal FreeShippingThreshold { get; set; } = 150000;
    public int LoyaltyEarnPerVnd { get; set; } = 10000;
    public int LoyaltyRedeemValueVnd { get; set; } = 100;
    public int ReservationHoldMinutes { get; set; } = 30;

    public bool IsQrConfigured =>
        !string.IsNullOrWhiteSpace(BankName) &&
        !string.IsNullOrWhiteSpace(BankAccountHolder) &&
        !string.IsNullOrWhiteSpace(QrImageUrl);
}

public class CartItemSessionDto
{
    public string LineId { get; set; } = Guid.NewGuid().ToString("N");
    public int ProductId { get; set; }
    public string? SizeName { get; set; }
    public string? Temperature { get; set; }
    public string? SugarLevel { get; set; }
    public string? IceLevel { get; set; }
    public List<int> ToppingIds { get; set; } = new();
    public int Quantity { get; set; } = 1;
    public string? Note { get; set; }
}

public class PricedLineItemDto
{
    public string LineId { get; set; } = string.Empty;
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public bool IsAvailable { get; set; }
    public string? UnavailableReason { get; set; }
    public string? SizeName { get; set; }
    public decimal SizePrice { get; set; }
    public string? Temperature { get; set; }
    public string? SugarLevel { get; set; }
    public string? IceLevel { get; set; }
    public List<(int Id, string Name, decimal Price)> Toppings { get; set; } = new();
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal { get; set; }
    public string? Note { get; set; }
}

public class CartSummaryDto
{
    public List<PricedLineItemDto> Items { get; set; } = new();
    public int TotalQuantity => Items.Sum(x => x.Quantity);
    public decimal Subtotal { get; set; }
    public string? PromoCode { get; set; }
    public int? PromotionId { get; set; }
    public string? PromoMessage { get; set; }
    public bool PromoValid { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal ShippingFee { get; set; }
    public decimal Total { get; set; }
    public bool HasUnavailableItems => Items.Any(x => !x.IsAvailable);
}

public class PromotionValidationResult
{
    public bool IsValid { get; set; }
    public string Message { get; set; } = string.Empty;
    public int? PromotionId { get; set; }
    public string? Code { get; set; }
    public decimal DiscountAmount { get; set; }
}

public class LoginViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập email.")]
    [EmailAddress(ErrorMessage = "Định dạng email không hợp lệ.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public bool RememberMe { get; set; }
    public string? ReturnUrl { get; set; }
}

public class RegisterViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập họ và tên.")]
    [MaxLength(120)]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập email.")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại.")]
    [RegularExpression(@"^(0|\+84)(3|5|7|8|9)\d{8}$", ErrorMessage = "Số điện thoại Việt Nam không hợp lệ (ví dụ: 0912345678).")]
    public string Phone { get; set; } = string.Empty;

    [MaxLength(300)]
    public string? Address { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
    [MinLength(8, ErrorMessage = "Mật khẩu phải từ 8 ký tự trở lên, gồm chữ hoa, chữ thường và chữ số.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng xác nhận mật khẩu.")]
    [Compare(nameof(Password), ErrorMessage = "Mật khẩu xác nhận không khớp.")]
    [DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class ChangePasswordViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập mật khẩu hiện tại.")]
    [DataType(DataType.Password)]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới.")]
    [MinLength(8, ErrorMessage = "Mật khẩu mới tối thiểu 8 ký tự gồm chữ hoa, chữ thường và số.")]
    [DataType(DataType.Password)]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng xác nhận mật khẩu mới.")]
    [Compare(nameof(NewPassword), ErrorMessage = "Xác nhận mật khẩu mới không khớp.")]
    [DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class ProfileUpdateViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập họ tên.")]
    [MaxLength(120)]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại.")]
    [RegularExpression(@"^(0|\+84)(3|5|7|8|9)\d{8}$", ErrorMessage = "Số điện thoại Việt Nam không hợp lệ.")]
    public string Phone { get; set; } = string.Empty;

    [MaxLength(300)]
    public string? Address { get; set; }

    public IFormFile? AvatarFile { get; set; }
}

public class OrderRequestLineDto
{
    public int ProductId { get; set; }
    public string? SizeName { get; set; }
    public string? Temperature { get; set; }
    public string? SugarLevel { get; set; }
    public string? IceLevel { get; set; }
    public List<int> ToppingIds { get; set; } = new();
    public int Quantity { get; set; } = 1;
    public string? Note { get; set; }
}

public class CreateOrderRequestDto
{
    public string? CustomerId { get; set; }
    public string? StaffId { get; set; }
    public string ActorDisplayName { get; set; } = "Khách hàng";
    public int? TableId { get; set; }
    public OrderType OrderType { get; set; } = OrderType.DineIn;
    public OrderSource Source { get; set; } = OrderSource.Online;
    public string PaymentMethod { get; set; } = "CASH";
    public bool MarkPaidImmediately { get; set; } = false;
    public string CustomerName { get; set; } = "Khách lẻ";
    public string? Phone { get; set; }
    public string? DeliveryAddress { get; set; }
    public string? PromoCode { get; set; }
    public string? Note { get; set; }
    public DateTime? OverrideCreatedAtUtc { get; set; } // Chỉ phục vụ Seeder tạo đơn demo trong 30 ngày qua
    public List<OrderRequestLineDto> Lines { get; set; } = new();
}

public class Table3DDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Zone { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public TableStatus Status { get; set; }
    public string StatusText { get; set; } = string.Empty;
    public double PosX { get; set; }
    public double PosY { get; set; }
    public int? ActiveOrderId { get; set; }
    public string? ActiveOrderCode { get; set; }
    public decimal? ActiveOrderTotal { get; set; }
    public string? ActiveOrderStatus { get; set; }
    public string? UpcomingReservationInfo { get; set; }
}

