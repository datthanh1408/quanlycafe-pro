using System.ComponentModel.DataAnnotations;
using CoffeeShop.Models.Entities;
using CoffeeShop.Models.Enums;

namespace CoffeeShop.Models.ViewModels;

public class ProductCardVm
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public decimal DisplayPrice { get; set; }
    public bool IsAvailable { get; set; }
    public bool IsFeatured { get; set; }
    public int SoldCount { get; set; }
    public string? DefaultSizeName { get; set; }
    public string LiquidColorHex { get; set; } = "#6F4E37";
}

public class HomeIndexVm
{
    public StoreSettingsDto Store { get; set; } = new();
    public List<Category> Categories { get; set; } = new();
    public List<ProductCardVm> FeaturedProducts { get; set; } = new();
    public List<Promotion> ActivePromotions { get; set; } = new();
    public List<Table3DDto> Tables3D { get; set; } = new();
    public int AvailableTableCount => Tables3D.Count(t => t.Status == TableStatus.Available);
}

public class MenuFilterVm
{
    public string? Keyword { get; set; }
    public int? CategoryId { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public bool OnlyAvailable { get; set; }
    public string Sort { get; set; } = "popular"; // popular, newest, price_asc, price_desc
    public int Page { get; set; } = 1;
    public List<Category> Categories { get; set; } = new();
    public PagedResult<ProductCardVm> Products { get; set; } = new();
}

public class ProductDetailVm
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public decimal BasePrice { get; set; }
    public bool IsAvailable { get; set; }
    public bool AllowTemperature { get; set; }
    public bool AllowSugar { get; set; }
    public bool AllowIce { get; set; }
    public string LiquidColorHex { get; set; } = "#6F4E37";
    public List<(string SizeName, decimal Price)> Sizes { get; set; } = new();
    public List<(int Id, string Name, decimal Price)> Toppings { get; set; } = new();
    public List<ProductCardVm> RelatedProducts { get; set; } = new();
}

public class CheckoutFormVm
{
    [Required(ErrorMessage = "Vui lòng nhập họ tên người nhận.")]
    [MaxLength(120)]
    public string CustomerName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại liên hệ.")]
    [RegularExpression(@"^(0|\+84)(3|5|7|8|9)\d{8}$", ErrorMessage = "Số điện thoại Việt Nam không hợp lệ.")]
    public string Phone { get; set; } = string.Empty;

    public OrderType OrderType { get; set; } = OrderType.TakeAway;

    public int? TableId { get; set; }

    [MaxLength(300)]
    public string? DeliveryAddress { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn phương thức thanh toán.")]
    public string PaymentMethod { get; set; } = "CASH";

    public string? PromoCode { get; set; }

    [MaxLength(200, ErrorMessage = "Ghi chú tối đa 200 ký tự.")]
    public string? Note { get; set; }

    public string IdempotencyToken { get; set; } = Guid.NewGuid().ToString("N");
}

public class CheckoutPageVm
{
    public CheckoutFormVm Form { get; set; } = new();
    public CartSummaryDto Cart { get; set; } = new();
    public StoreSettingsDto Store { get; set; } = new();
    public List<PaymentMethodConfig> PaymentMethods { get; set; } = new();
    public List<Table3DDto> Tables3D { get; set; } = new();
}

public class ReservationCreateVm
{
    [Required(ErrorMessage = "Vui lòng chọn bàn muốn đặt.")]
    public int TableId { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập họ tên.")]
    [MaxLength(120)]
    public string CustomerName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại.")]
    [RegularExpression(@"^(0|\+84)(3|5|7|8|9)\d{8}$", ErrorMessage = "Số điện thoại Việt Nam không hợp lệ.")]
    public string Phone { get; set; } = string.Empty;

    [Range(1, 20, ErrorMessage = "Số lượng khách từ 1 đến 20 người.")]
    public int PartySize { get; set; } = 2;

    [Required(ErrorMessage = "Vui lòng chọn thời gian đến.")]
    public DateTime ReservedAtLocal { get; set; }

    [MaxLength(200)]
    public string? Note { get; set; }
}

