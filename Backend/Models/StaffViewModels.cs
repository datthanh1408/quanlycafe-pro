using System.ComponentModel.DataAnnotations;
using CoffeeShop.Models.Entities;
using CoffeeShop.Models.Enums;
using CoffeeShop.Models.ViewModels;

namespace CoffeeShop.Areas.Staff.Models;

// ============================================================================
// 1. DASHBOARD & CA LÀM VIỆC (/staff/dashboard)
// ============================================================================

public class StaffDashboardVm
{
    public string StaffName { get; set; } = string.Empty;
    public Shift? ActiveShift { get; set; }
    public Shift? LastClosedShiftToday { get; set; }
    public decimal LiveCashCollected { get; set; }
    public decimal LiveExpectedCash { get; set; }
    public int OpenOrdersCount { get; set; }

    public string PeriodLabel { get; set; } = "Hôm nay";
    public int TotalOrdersInShift { get; set; }
    public int PendingOrdersCount { get; set; }
    public int ProcessingOrdersCount { get; set; }
    public int CompletedOrdersCount { get; set; }

    public bool CanViewRevenue { get; set; }
    public decimal? ShiftRevenue { get; set; }

    public List<StaffOrderListItemVm> LatestOrders { get; set; } = new();
}

public class OpenShiftInputDto
{
    [Range(0, 1000000000, ErrorMessage = "Tiền đầu ca không hợp lệ.")]
    public decimal OpeningCash { get; set; }

    [MaxLength(200)]
    public string? Note { get; set; }
}

public class CloseShiftInputDto
{
    [Range(0, 1000000000, ErrorMessage = "Tiền thực đếm không hợp lệ.")]
    public decimal CountedCash { get; set; }

    [MaxLength(200)]
    public string? Note { get; set; }
}

// ============================================================================
// 2. ĐƠN HÀNG & HÓA ĐƠN (/staff/orders)
// ============================================================================

public class StaffOrderListItemVm
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public OrderType OrderType { get; set; }
    public OrderSource Source { get; set; }
    public OrderStatus Status { get; set; }
    public PaymentStatus PaymentStatus { get; set; }
    public string PaymentMethod { get; set; } = "CASH";
    public string? TableCode { get; set; }
    public int ItemCount { get; set; }
    public decimal Total { get; set; }
    public DateTime CreatedAt { get; set; }
    public OrderStatus? NextForwardStatus { get; set; }
    public string? RowVersionBase64 { get; set; }
}

public class StaffOrderListVm
{
    public PagedResult<StaffOrderListItemVm> Orders { get; set; } = new();
    public OrderStatus? CurrentStatus { get; set; }
    public string? SearchKeyword { get; set; }
    public string? DateFilter { get; set; }
    public string BaseQueryUrl { get; set; } = "/staff/orders";
    public Dictionary<OrderStatus, int> StatusCounts { get; set; } = new();
    public int AllCount { get; set; }
}

public class StaffOrderDetailVm
{
    public Order Order { get; set; } = null!;
    public StoreSettingsDto StoreSettings { get; set; } = new();
    public List<OrderStatus> StatusFlow { get; set; } = new();
    public OrderStatus? NextForwardStatus { get; set; }
    public OrderStatus? PrevRollbackStatus { get; set; }
    public bool IsAdmin { get; set; }
    public bool CanCancelThisOrder { get; set; }
    public string RowVersionBase64 { get; set; } = string.Empty;
}

public class StaffOrderReceiptVm
{
    public Order Order { get; set; } = null!;
    public StoreSettingsDto StoreSettings { get; set; } = new();
    public string CashierName { get; set; } = string.Empty;
    public bool FromPos { get; set; }
}

// ============================================================================
// 3. SƠ ĐỒ BÀN 3D & ĐẶT BÀN (/staff/tables & /staff/reservations)
// ============================================================================

public class StaffTablesVm
{
    public List<Table3DDto> Tables { get; set; } = new();
    public string TablesJson { get; set; } = "[]";
    public int? SelectedTableId { get; set; }
    public int AvailableCount { get; set; }
    public int OccupiedCount { get; set; }
    public int ReservedCount { get; set; }
}

public class StaffReservationItemVm
{
    public int Id { get; set; }
    public int TableId { get; set; }
    public string TableCode { get; set; } = string.Empty;
    public string Zone { get; set; } = string.Empty;
    public int TableCapacity { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public int PartySize { get; set; }
    public DateTime ReservedAt { get; set; }
    public ReservationStatus Status { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class StaffReservationsVm
{
    public PagedResult<StaffReservationItemVm> Reservations { get; set; } = new();
    public ReservationStatus? CurrentStatus { get; set; }
    public string? DateFilter { get; set; }
    public string? SearchKeyword { get; set; }
    public string BaseQueryUrl { get; set; } = "/staff/reservations";
    public int PendingCount { get; set; }
    public int ConfirmedTodayCount { get; set; }
}

// ============================================================================
// 4. BÁN HÀNG TẠI QUẦY POS (/staff/pos)
// ============================================================================

public class PosSizeItemDto
{
    public string SizeName { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

public class PosToppingItemDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

public class PosProductClientDto
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public decimal BasePrice { get; set; }
    public bool IsAvailable { get; set; }
    public bool AllowTemperature { get; set; }
    public bool AllowSugar { get; set; }
    public bool AllowIce { get; set; }
    public List<PosSizeItemDto> Sizes { get; set; } = new();
    public List<PosToppingItemDto> Toppings { get; set; } = new();
}

public class PosValidatePromoDto
{
    public string? Code { get; set; }
    public decimal Subtotal { get; set; }
}

public class PosSubmitOrderDto
{
    public OrderType OrderType { get; set; } = OrderType.DineIn;
    public int? TableId { get; set; }
    public int? AppendToOpenOrderId { get; set; }
    public string? CustomerName { get; set; }
    public string? Phone { get; set; }
    public string PaymentMethod { get; set; } = "CASH";
    public bool MarkPaidImmediately { get; set; }
    public string? PromoCode { get; set; }
    public string? Note { get; set; }
    public List<OrderRequestLineDto> Lines { get; set; } = new();
}

public class StaffPosVm
{
    public List<Category> Categories { get; set; } = new();
    public List<Table3DDto> Tables { get; set; } = new();
    public List<PaymentMethodConfig> PaymentMethods { get; set; } = new();
    public StoreSettingsDto StoreSettings { get; set; } = new();
    public string ProductsJson { get; set; } = "[]";
    public string TablesJson { get; set; } = "[]";
    public int? SelectedTableId { get; set; }
    public string? PrefillCustomerName { get; set; }
    public string? PrefillPhone { get; set; }
    public bool CanApplyPromotion { get; set; }
}

// ============================================================================
// 5. KHÁCH HÀNG, TRẠNG THÁI MÓN & HỒ SƠ CA (/staff/customers, /availability, /profile)
// ============================================================================

public class StaffCustomerRecentOrderVm
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public decimal Total { get; set; }
    public OrderStatus Status { get; set; }
    public PaymentStatus PaymentStatus { get; set; }
}

public class StaffCustomerItemVm
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public int LoyaltyPoints { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public int OrderCount { get; set; }
    public decimal TotalSpent { get; set; }
    public List<StaffCustomerRecentOrderVm> RecentOrders { get; set; } = new();
}

public class StaffCustomerListVm
{
    public PagedResult<StaffCustomerItemVm> Customers { get; set; } = new();
    public string? SearchKeyword { get; set; }
    public string BaseQueryUrl { get; set; } = "/staff/customers";
    public bool CanEditCustomer { get; set; }
}

public class StaffEditCustomerInputDto
{
    [Required]
    public string Id { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập họ tên khách hàng.")]
    [MaxLength(120)]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại.")]
    [RegularExpression(@"^(0|\+84)(3|5|7|8|9)\d{8}$", ErrorMessage = "Số điện thoại Việt Nam không hợp lệ.")]
    public string Phone { get; set; } = string.Empty;

    [MaxLength(300)]
    public string? Address { get; set; }

    public string? ReturnQuery { get; set; }
}

public class StaffAvailabilityItemVm
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public decimal BasePrice { get; set; }
    public bool IsAvailable { get; set; }
    public int SoldCount { get; set; }
}

public class StaffAvailabilityVm
{
    public List<Category> Categories { get; set; } = new();
    public List<StaffAvailabilityItemVm> Products { get; set; } = new();
    public int? CategoryId { get; set; }
    public string? SearchKeyword { get; set; }
    public string FilterState { get; set; } = "all";
    public int AvailableCount { get; set; }
    public int UnavailableCount { get; set; }
}

public class StaffPermissionStatusVm
{
    public string ClaimValue { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsGranted { get; set; }
}

public class StaffProfileVm
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? AvatarUrl { get; set; }
    public List<string> Roles { get; set; } = new();
    public string Position { get; set; } = string.Empty;
    public string WorkStatus { get; set; } = string.Empty;
    public DateTime HireDate { get; set; }
    public List<StaffPermissionStatusVm> Permissions { get; set; } = new();
    public Shift? ActiveShift { get; set; }
    public List<Shift> RecentShifts { get; set; } = new();
}
