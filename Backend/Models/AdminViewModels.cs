using System.ComponentModel.DataAnnotations;
using CoffeeShop.Authorization;
using CoffeeShop.Models.Entities;
using CoffeeShop.Models.Enums;
using CoffeeShop.Models.ViewModels;
using CoffeeShop.Services;

namespace CoffeeShop.Areas.Admin.Models;

// =========================================================================
// 1. DASHBOARD VIEWMODELS
// =========================================================================
public class AdminDashboardViewModel
{
    public string Range { get; set; } = "30d"; // today, 7d, 30d, custom
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string PeriodLabel { get; set; } = string.Empty;
    public DashboardSummaryDto Summary { get; set; } = new();
}

// =========================================================================
// 2. CATALOG VIEWMODELS (CATEGORIES, TOPPINGS, PRODUCTS & RECIPES)
// =========================================================================
public class CategoryListItemVm
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string IconClass { get; set; } = "bi-cup-hot";
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; }
    public int ProductCount { get; set; }
}

public class CategoryFormModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên danh mục.")]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(120)]
    public string? Slug { get; set; }

    [MaxLength(80)]
    public string IconClass { get; set; } = "bi-cup-hot";

    public int DisplayOrder { get; set; } = 0;
    public bool IsActive { get; set; } = true;
    public string? ExistingImageUrl { get; set; }
    public IFormFile? ImageFile { get; set; }
}

public class CategoriesAdminViewModel
{
    public List<CategoryListItemVm> Categories { get; set; } = new();
    public CategoryFormModel Form { get; set; } = new();
}

public class ToppingFormModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên topping.")]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Range(1000, 1000000, ErrorMessage = "Giá topping phải lớn hơn 0 (tối thiểu 1.000 ₫).")]
    public decimal Price { get; set; } = 10000;

    public bool IsActive { get; set; } = true;
}

public class ToppingsAdminViewModel
{
    public List<Topping> Toppings { get; set; } = new();
    public ToppingFormModel Form { get; set; } = new();
}

public class ProductSizeRowVm
{
    [MaxLength(20)]
    public string SizeName { get; set; } = string.Empty;

    public decimal Price { get; set; }
    public int DisplayOrder { get; set; }
}

public class ProductRecipeRowVm
{
    public int IngredientId { get; set; }

    [MaxLength(20)]
    public string? SizeName { get; set; }

    public decimal Quantity { get; set; }
}

public class ProductFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên sản phẩm.")]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn danh mục sản phẩm.")]
    public int CategoryId { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Range(1000, 10000000, ErrorMessage = "Giá cơ bản phải lớn hơn 0 (tối thiểu 1.000 ₫).")]
    public decimal BasePrice { get; set; } = 35000;

    public string ExistingImageUrl { get; set; } = "/images/seed/default-drink.svg";
    public IFormFile? ImageFile { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn màu chất lỏng 3D.")]
    [MaxLength(20)]
    public string LiquidColorHex { get; set; } = "#4A2C18";

    public bool AllowTemperature { get; set; } = true;
    public bool AllowSugar { get; set; } = true;
    public bool AllowIce { get; set; } = true;
    public bool IsFeatured { get; set; } = false;
    public bool IsAvailable { get; set; } = true;

    public List<ProductSizeRowVm> Sizes { get; set; } = new();
    public List<int> SelectedToppingIds { get; set; } = new();
    public List<ProductRecipeRowVm> Recipes { get; set; } = new();

    // Dữ liệu tham chiếu cho Form
    public List<Category> Categories { get; set; } = new();
    public List<Topping> AllToppings { get; set; } = new();
    public List<Ingredient> AllIngredients { get; set; } = new();
}

public class ProductsAdminViewModel
{
    public string? Search { get; set; }
    public int? CategoryId { get; set; }
    public bool? IsAvailable { get; set; }
    public PagedResult<Product> PagedProducts { get; set; } = new();
    public List<Category> Categories { get; set; } = new();
    public string BaseQueryUrl { get; set; } = "/admin/products";
}

// =========================================================================
// 3. TABLES 3D & CRUD VIEWMODELS
// =========================================================================
public class TablePositionUpdateRequest
{
    public int Id { get; set; }
    public double PosX { get; set; }
    public double PosY { get; set; }
    public string? Zone { get; set; }
}

public class CafeTableFormModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập mã bàn (VD: T1-01).")]
    [MaxLength(30)]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng chọn khu vực.")]
    [MaxLength(80)]
    public string Zone { get; set; } = "Tầng 1";

    [Range(1, 20, ErrorMessage = "Sức chứa hợp lệ từ 1 đến 20 khách.")]
    public int Capacity { get; set; } = 4;

    [Range(-12.0, 12.0, ErrorMessage = "Tọa độ X nằm trong khoảng -12 đến 12.")]
    public double PosX { get; set; } = 0;

    [Range(-12.0, 12.0, ErrorMessage = "Tọa độ Y nằm trong khoảng -12 đến 12.")]
    public double PosY { get; set; } = 0;

    public bool IsActive { get; set; } = true;
}

public class TablesAdminViewModel
{
    public string? ZoneFilter { get; set; }
    public List<CafeTable> Tables { get; set; } = new();
    public List<Table3DDto> Tables3D { get; set; } = new();
    public HashSet<int> TableIdsWithOpenOrder { get; set; } = new();
    public CafeTableFormModel Form { get; set; } = new();
}

// =========================================================================
// 4. ORDERS FILTER & DETAIL VIEWMODELS
// =========================================================================
public class OrdersAdminViewModel
{
    public string? Search { get; set; }
    public OrderStatus? Status { get; set; }
    public PaymentStatus? PaymentStatus { get; set; }
    public OrderType? OrderType { get; set; }
    public string? StaffId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public PagedResult<Order> PagedOrders { get; set; } = new();
    public List<ApplicationUser> StaffMembers { get; set; } = new();
    public string BaseQueryUrl { get; set; } = "/admin/orders";
}

public class OrderAdminDetailViewModel
{
    public Order Order { get; set; } = null!;
    public OrderStatus? NextStatus { get; set; }
    public OrderStatus? RollbackStatus { get; set; }
    public string RowVersionBase64 { get; set; } = string.Empty;
}

// =========================================================================
// 5. INVENTORY & STOCK TRANSACTIONS VIEWMODELS
// =========================================================================
public class IngredientFormModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập mã nguyên liệu (VD: NL-001).")]
    [MaxLength(30)]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập tên nguyên liệu.")]
    [MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập đơn vị tính.")]
    [MaxLength(30)]
    public string Unit { get; set; } = "g";

    [Range(0, 100000000, ErrorMessage = "Định mức cảnh báo không được âm.")]
    public decimal MinThreshold { get; set; } = 0;

    public bool IsActive { get; set; } = true;
}

public class StockTransactionFormModel
{
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn nguyên liệu.")]
    public int IngredientId { get; set; }

    public StockTransactionType Type { get; set; } = StockTransactionType.Import;
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; } = 0;

    [Required(ErrorMessage = "Vui lòng nhập lý do thực hiện giao dịch kho.")]
    [MaxLength(200)]
    public string Reason { get; set; } = string.Empty;
}

public class InventoryAdminViewModel
{
    public string? Search { get; set; }
    public bool LowStockOnly { get; set; }
    public List<Ingredient> Ingredients { get; set; } = new();
    public List<Ingredient> AllIngredientsDropdown { get; set; } = new();

    public int? TxIngredientId { get; set; }
    public StockTransactionType? TxType { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public PagedResult<StockTransaction> PagedTransactions { get; set; } = new();
    public string BaseQueryUrl { get; set; } = "/admin/inventory";
}

// =========================================================================
// 6. PROMOTIONS VIEWMODELS
// =========================================================================
public class PromotionFormModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập mã khuyến mãi.")]
    [MaxLength(40)]
    [RegularExpression(@"^[A-Za-z0-9\-]+$", ErrorMessage = "Mã chỉ gồm chữ không dấu, số và dấu gạch ngang (-).")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập tên chương trình khuyến mãi.")]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    public PromotionType Type { get; set; } = PromotionType.Percentage;
    public decimal Value { get; set; } = 10;
    public decimal? MaxDiscount { get; set; }
    public decimal MinOrderValue { get; set; } = 0;

    [Required(ErrorMessage = "Vui lòng chọn thời điểm bắt đầu.")]
    public DateTime StartAt { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn thời điểm kết thúc.")]
    public DateTime EndAt { get; set; }

    [Range(1, 1000000, ErrorMessage = "Tổng giới hạn lượt dùng phải >= 1.")]
    public int UsageLimit { get; set; } = 500;

    [Range(1, 10000, ErrorMessage = "Giới hạn mỗi khách hàng phải >= 1.")]
    public int PerUserLimit { get; set; } = 3;

    public bool IsActive { get; set; } = true;
}

public class PromotionsAdminViewModel
{
    public string? Search { get; set; }
    public List<Promotion> Promotions { get; set; } = new();
    public PromotionFormModel Form { get; set; } = new();
}

// =========================================================================
// 7. EMPLOYEES & CUSTOMERS VIEWMODELS
// =========================================================================
public class EmployeeRowVm
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string Role { get; set; } = AppRoles.Staff;
    public bool IsActive { get; set; }
    public bool MustChangePassword { get; set; }
    public string Position { get; set; } = "Barista / Thu ngân";
    public DateTime HireDate { get; set; } = DateTime.UtcNow;
    public string WorkStatus { get; set; } = "Đang làm";
    public List<string> GrantedPermissions { get; set; } = new();
}

public class CreateEmployeeFormModel
{
    [Required(ErrorMessage = "Vui lòng nhập họ và tên nhân viên.")]
    [MaxLength(120)]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập email đăng nhập.")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    public string Email { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? Phone { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu tạm thời.")]
    [MinLength(8, ErrorMessage = "Mật khẩu tạm phải từ 8 ký tự (có chữ hoa, chữ thường, số).")]
    public string TemporaryPassword { get; set; } = "Staff@123";

    [Required, MaxLength(100)]
    public string Position { get; set; } = "Barista / Thu ngân";

    public DateTime HireDate { get; set; } = DateTime.Today;

    [Required, MaxLength(50)]
    public string WorkStatus { get; set; } = "Đang làm";

    public List<string> Permissions { get; set; } = new();
}

public class EditEmployeeFormModel
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập họ tên.")]
    [MaxLength(120)]
    public string FullName { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? Phone { get; set; }

    [Required, MaxLength(100)]
    public string Position { get; set; } = "Barista / Thu ngân";

    public DateTime HireDate { get; set; } = DateTime.Today;

    [Required, MaxLength(50)]
    public string WorkStatus { get; set; } = "Đang làm";

    public List<string> Permissions { get; set; } = new();
}

public class EmployeesAdminViewModel
{
    public List<EmployeeRowVm> Employees { get; set; } = new();
    public string CurrentUserId { get; set; } = string.Empty;
    public int ActiveAdminCount { get; set; }
}

public class CustomerRowVm
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; }
    public int LoyaltyPoints { get; set; }
    public DateTime CreatedAt { get; set; }
    public int CompletedOrderCount { get; set; }
    public decimal TotalSpend { get; set; }
}

public class CustomersAdminViewModel
{
    public string? Search { get; set; }
    public string Sort { get; set; } = "newest"; // newest, spend_desc, orders_desc
    public PagedResult<CustomerRowVm> PagedCustomers { get; set; } = new();
    public string BaseQueryUrl { get; set; } = "/admin/customers";
}

public class CustomerDetailViewModel
{
    public CustomerRowVm Customer { get; set; } = new();
    public List<Order> Orders { get; set; } = new();
}

// =========================================================================
// 8. REPORTS, SETTINGS & AUDIT LOGS VIEWMODELS
// =========================================================================
public class StockMovementSummaryVm
{
    public int IngredientId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal ImportedQty { get; set; }
    public decimal SaleConsumedQty { get; set; }
    public decimal ExportAndWastageQty { get; set; }
    public decimal AdjustDeltaQty { get; set; }
    public decimal CurrentStock { get; set; }
    public decimal MinThreshold { get; set; }
    public bool IsLowStock => CurrentStock <= MinThreshold;
}

public class ReportsAdminViewModel
{
    public string Preset { get; set; } = "month"; // today, 7d, month, year, custom
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public DashboardSummaryDto Summary { get; set; } = new();
    public List<StockMovementSummaryVm> StockMovements { get; set; } = new();
}

public class SettingsFormModel
{
    public string ActiveTab { get; set; } = "tab-store";

    // Tab 1: Thông tin quán & Giờ mở cửa
    [Required(ErrorMessage = "Vui lòng nhập tên quán."), MaxLength(120)]
    public string ShopName { get; set; } = "Đạt Mộc Coffee";

    [MaxLength(200)]
    public string Slogan { get; set; } = string.Empty;

    [MaxLength(100)]
    public string BusinessType { get; set; } = "Cafe & Bakery";

    [MaxLength(150)]
    public string OpeningHours { get; set; } = "07:00 – 22:00 hằng ngày";

    [MaxLength(300)]
    public string Address { get; set; } = string.Empty;

    public string ExistingLogoUrl { get; set; } = string.Empty;
    public IFormFile? LogoFile { get; set; }

    // Tab 2: Màu thương hiệu
    [Required, MaxLength(20)]
    public string PrimaryColor { get; set; } = "#6F4E37";

    [Required, MaxLength(20)]
    public string SecondaryColor { get; set; } = "#F6EFE7";

    [Required, MaxLength(20)]
    public string AccentColor { get; set; } = "#C8782A";

    // Tab 3: Liên hệ, MXH & Bản đồ
    [MaxLength(30)]
    public string Phone { get; set; } = string.Empty;

    [MaxLength(120)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(300)]
    public string FacebookUrl { get; set; } = string.Empty;

    [MaxLength(300)]
    public string ZaloUrl { get; set; } = string.Empty;

    [MaxLength(600)]
    public string GoogleMapsEmbedUrl { get; set; } = string.Empty;

    // Tab 4: Chuyển khoản Ngân hàng & QR
    [MaxLength(120)]
    public string BankName { get; set; } = string.Empty;

    [MaxLength(50)]
    public string BankAccountNumber { get; set; } = string.Empty;

    [MaxLength(120)]
    public string BankAccountHolder { get; set; } = string.Empty;

    public string ExistingQrImageUrl { get; set; } = string.Empty;
    public IFormFile? QrImageFile { get; set; }

    // Tab 5: Giao hàng, Tích điểm, Đặt bàn
    [Range(0, 500000)]
    public decimal ShippingFee { get; set; } = 15000;

    [Range(0, 10000000)]
    public decimal FreeShippingThreshold { get; set; } = 150000;

    [Range(1000, 1000000)]
    public int LoyaltyEarnPerVnd { get; set; } = 10000;

    [Range(1, 100000)]
    public int LoyaltyRedeemValueVnd { get; set; } = 100;

    [Range(5, 240)]
    public int ReservationHoldMinutes { get; set; } = 30;
}

public class SettingsAdminViewModel
{
    public SettingsFormModel Form { get; set; } = new();
    public List<PaymentMethodConfig> PaymentMethods { get; set; } = new();
    public bool IsQrConfigured { get; set; }
}

public class AuditLogsAdminViewModel
{
    public string? UserEmail { get; set; }
    public string? Action { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public PagedResult<AuditLog> PagedLogs { get; set; } = new();
    public List<string> DistinctActions { get; set; } = new();
    public string BaseQueryUrl { get; set; } = "/admin/audit-logs";
}
