using System.ComponentModel.DataAnnotations;

namespace CoffeeShop.Models.Enums;

public enum TableStatus
{
    [Display(Name = "Trống")]
    Available = 0,

    [Display(Name = "Đang sử dụng")]
    Occupied = 1,

    [Display(Name = "Đã đặt trước")]
    Reserved = 2
}

public enum ReservationStatus
{
    [Display(Name = "Chờ xác nhận")]
    Pending = 0,

    [Display(Name = "Đã xác nhận")]
    Confirmed = 1,

    [Display(Name = "Đã hủy")]
    Cancelled = 2,

    [Display(Name = "Khách đã đến")]
    Completed = 3
}

public enum OrderType
{
    [Display(Name = "Tại quán")]
    DineIn = 0,

    [Display(Name = "Mang đi")]
    TakeAway = 1,

    [Display(Name = "Giao hàng")]
    Delivery = 2
}

public enum OrderSource
{
    [Display(Name = "Online")]
    Online = 0,

    [Display(Name = "POS")]
    POS = 1
}

public enum OrderStatus
{
    [Display(Name = "Chờ xác nhận")]
    Pending = 0,

    [Display(Name = "Đã xác nhận")]
    Confirmed = 1,

    [Display(Name = "Đang pha chế")]
    Brewing = 2,

    [Display(Name = "Sẵn sàng nhận")]
    Ready = 3,

    [Display(Name = "Đang giao")]
    Delivering = 4,

    [Display(Name = "Hoàn thành")]
    Completed = 5,

    [Display(Name = "Đã hủy")]
    Cancelled = 6
}

public enum PaymentStatus
{
    [Display(Name = "Chưa thanh toán")]
    Unpaid = 0,

    [Display(Name = "Đã thanh toán")]
    Paid = 1,

    [Display(Name = "Đã hoàn tiền")]
    Refunded = 2
}

public enum PromotionType
{
    [Display(Name = "Phần trăm (%)")]
    Percentage = 0,

    [Display(Name = "Số tiền (₫)")]
    FixedAmount = 1
}

public enum StockTransactionType
{
    [Display(Name = "Nhập kho")]
    Import = 0,

    [Display(Name = "Xuất kho")]
    Export = 1,

    [Display(Name = "Điều chỉnh")]
    Adjust = 2,

    [Display(Name = "Bán hàng")]
    Sale = 3,

    [Display(Name = "Hao hụt")]
    Wastage = 4
}

public static class EnumExtensions
{
    public static string GetDisplayName(this Enum value)
    {
        var field = value.GetType().GetField(value.ToString());
        if (field == null) return value.ToString();
        var attr = Attribute.GetCustomAttribute(field, typeof(DisplayAttribute)) as DisplayAttribute;
        return attr?.Name ?? value.ToString();
    }

    public static string GetBadgeColor(this OrderStatus status) => status switch
    {
        OrderStatus.Pending => "#F59E0B",
        OrderStatus.Confirmed => "#3B82F6",
        OrderStatus.Brewing => "#8B5CF6",
        OrderStatus.Ready => "#10B981",
        OrderStatus.Delivering => "#06B6D4",
        OrderStatus.Completed => "#64748B",
        OrderStatus.Cancelled => "#EF4444",
        _ => "#64748B"
    };

    public static string GetStatusIcon(this OrderStatus status) => status switch
    {
        OrderStatus.Pending => "bi-hourglass-split",
        OrderStatus.Confirmed => "bi-check2-circle",
        OrderStatus.Brewing => "bi-cup-hot-fill",
        OrderStatus.Ready => "bi-bell-fill",
        OrderStatus.Delivering => "bi-bicycle",
        OrderStatus.Completed => "bi-patch-check-fill",
        OrderStatus.Cancelled => "bi-x-octagon-fill",
        _ => "bi-circle"
    };
}

