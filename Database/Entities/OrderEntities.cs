using System.ComponentModel.DataAnnotations;
using CoffeeShop.Models.Enums;

namespace CoffeeShop.Models.Entities;

public class CafeTable
{
    public int Id { get; set; }

    [Required, MaxLength(30)]
    public string Code { get; set; } = string.Empty; // T1-01, T2-01, SV-01

    [Required, MaxLength(80)]
    public string Zone { get; set; } = "Tầng 1"; // Tầng 1, Tầng 2, Sân vườn

    public int Capacity { get; set; } = 4;
    public TableStatus Status { get; set; } = TableStatus.Available;

    // Tọa độ không gian sàn 3D Three.js (-8.0 .. +8.0 mét trong cảnh)
    public double PosX { get; set; } = 0;
    public double PosY { get; set; } = 0;

    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; } = false;

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ICollection<Order> Orders { get; set; } = new List<Order>();
    public ICollection<TableReservation> Reservations { get; set; } = new List<TableReservation>();
}

public class TableReservation
{
    public int Id { get; set; }

    public int TableId { get; set; }
    public CafeTable Table { get; set; } = null!;

    [Required, MaxLength(120)]
    public string CustomerName { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string Phone { get; set; } = string.Empty;

    public int PartySize { get; set; } = 2;
    public DateTime ReservedAt { get; set; }
    public ReservationStatus Status { get; set; } = ReservationStatus.Pending;

    [MaxLength(200)]
    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Order
{
    public int Id { get; set; }

    [Required, MaxLength(30)]
    public string Code { get; set; } = string.Empty; // CF-yyMMdd-NNNN

    public string? CustomerId { get; set; }
    public ApplicationUser? Customer { get; set; }

    public string? StaffId { get; set; }
    public ApplicationUser? Staff { get; set; }

    public int? TableId { get; set; }
    public CafeTable? Table { get; set; }

    public OrderType OrderType { get; set; } = OrderType.DineIn;
    public OrderSource Source { get; set; } = OrderSource.Online;
    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    [Required, MaxLength(30)]
    public string PaymentMethod { get; set; } = "CASH"; // CASH, QR, CARD

    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Unpaid;

    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal ShippingFee { get; set; }
    public decimal Total { get; set; }

    public int? PromotionId { get; set; }
    public Promotion? Promotion { get; set; }

    [Required, MaxLength(120)]
    public string CustomerName { get; set; } = "Khách lẻ";

    [MaxLength(20)]
    public string? Phone { get; set; }

    [MaxLength(300)]
    public string? DeliveryAddress { get; set; }

    [MaxLength(200)]
    public string? Note { get; set; }

    [MaxLength(200)]
    public string? CancelReason { get; set; }

    [MaxLength(120)]
    public string? CancelledBy { get; set; }
    public DateTime? CancelledAt { get; set; }

    [MaxLength(120)]
    public string? PaymentConfirmedBy { get; set; }
    public DateTime? PaidAt { get; set; }

    public int? ShiftId { get; set; }
    public Shift? Shift { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    public ICollection<OrderStatusHistory> StatusHistories { get; set; } = new List<OrderStatusHistory>();
}

public class OrderItem
{
    public int Id { get; set; }

    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    [Required, MaxLength(150)]
    public string ProductName { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? SizeName { get; set; }

    public decimal UnitPrice { get; set; } // Giá size + tổng topping tại thời điểm đặt

    [MaxLength(30)]
    public string? Temperature { get; set; } // Nóng / Lạnh

    [MaxLength(20)]
    public string? SugarLevel { get; set; } // 0%, 30%, 50%, 70%, 100%

    [MaxLength(20)]
    public string? IceLevel { get; set; } // 0%, 30%, 50%, 70%, 100%

    public int Quantity { get; set; } = 1;

    [MaxLength(200)]
    public string? Note { get; set; }

    public decimal LineTotal { get; set; }

    public ICollection<OrderItemTopping> Toppings { get; set; } = new List<OrderItemTopping>();
}

public class OrderItemTopping
{
    public int Id { get; set; }

    public int OrderItemId { get; set; }
    public OrderItem OrderItem { get; set; } = null!;

    [Required, MaxLength(100)]
    public string ToppingName { get; set; } = string.Empty;

    public decimal Price { get; set; }
}

public class OrderStatusHistory
{
    public int Id { get; set; }

    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;

    public OrderStatus FromStatus { get; set; }
    public OrderStatus ToStatus { get; set; }

    [Required, MaxLength(120)]
    public string ChangedBy { get; set; } = string.Empty;

    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(200)]
    public string? Note { get; set; }
}

