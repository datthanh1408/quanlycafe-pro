# Sơ Đồ Thực Thể Quan Hệ (ERD) – Đạt Mộc Coffee

Tài liệu mô tả mô hình cơ sở dữ liệu quan hệ (Microsoft SQL Server / LocalDB) quản lý qua Entity Framework Core Code-First Migrations cho hệ thống **Đạt Mộc Coffee**.

## 1. Sơ đồ ERD (Mermaid)

```mermaid
erDiagram
    ApplicationUser ||--o| EmployeeProfile : "1-0..1 hồ sơ nhân viên"
    ApplicationUser ||--o{ Order : "CustomerId (khách đặt)"
    ApplicationUser ||--o{ Order : "StaffId (nhân viên xử lý)"
    ApplicationUser ||--o{ PromotionUsage : "sử dụng mã"
    ApplicationUser ||--o{ Shift : "mở/đóng ca"
    ApplicationUser ||--o{ AuditLog : "thực hiện"

    Category ||--o{ Product : "chứa sản phẩm"
    Product ||--o{ ProductSize : "có các size"
    Product ||--o{ ProductTopping : "cho phép topping"
    Topping ||--o{ ProductTopping : "gắn với món"

    Product ||--o{ Recipe : "công thức pha chế"
    Ingredient ||--o{ Recipe : "định lượng nguyên liệu"
    Ingredient ||--o{ StockTransaction : "biến động tồn kho"

    CafeTable ||--o{ Order : "phục vụ đơn tại bàn"
    CafeTable ||--o{ TableReservation : "lịch đặt bàn"

    Promotion ||--o{ Order : "áp dụng giảm giá"
    Promotion ||--o{ PromotionUsage : "lịch sử lượt dùng"

    Shift ||--o{ Order : "ghi nhận trong ca"

    Order ||--o{ OrderItem : "chi tiết dòng món"
    Order ||--o{ OrderStatusHistory : "lịch sử trạng thái"
    Order ||--o{ StockTransaction : "trừ kho pha chế"
    OrderItem ||--o{ OrderItemTopping : "topping snapshot"

    ApplicationUser {
        string Id PK
        string UserName
        string Email UK
        string FullName
        string Phone
        string Address
        string AvatarUrl
        bool IsActive
        bool IsDeleted
        bool MustChangePassword
        int LoyaltyPoints
        datetime CreatedAt
    }

    EmployeeProfile {
        int Id PK
        string UserId FK,UK
        string Position
        datetime HireDate
        string WorkStatus
    }

    Category {
        int Id PK
        string Name UK
        string Slug UK
        string ImageUrl
        string IconClass
        int DisplayOrder
        bool IsActive
        bool IsDeleted
    }

    Product {
        int Id PK
        int CategoryId FK
        string Name
        string Description
        string ImageUrl
        decimal BasePrice
        bool AllowTemperature
        bool AllowSugar
        bool AllowIce
        bool IsAvailable
        bool IsFeatured
        int SoldCount
        bool IsDeleted
        datetime CreatedAt
    }

    ProductSize {
        int Id PK
        int ProductId FK
        string SizeName
        decimal Price
        int DisplayOrder
    }

    Topping {
        int Id PK
        string Name UK
        decimal Price
        bool IsActive
        bool IsDeleted
    }

    ProductTopping {
        int ProductId PK,FK
        int ToppingId PK,FK
    }

    CafeTable {
        int Id PK
        string Code UK
        string Zone
        int Capacity
        int Status
        double PosX
        double PosY
        bool IsActive
        bool IsDeleted
        byte[] RowVersion
    }

    TableReservation {
        int Id PK
        int TableId FK
        string CustomerName
        string Phone
        int PartySize
        datetime ReservedAt
        int Status
        string Note
        datetime CreatedAt
    }

    Order {
        int Id PK
        string Code UK
        string CustomerId FK
        string StaffId FK
        int TableId FK
        int OrderType
        int Source
        int Status
        string PaymentMethod
        int PaymentStatus
        decimal Subtotal
        decimal DiscountAmount
        decimal ShippingFee
        decimal Total
        int PromotionId FK
        string CustomerName
        string Phone
        string DeliveryAddress
        string Note
        string CancelReason
        string CancelledBy
        datetime CancelledAt
        int ShiftId FK
        datetime CreatedAt
        datetime UpdatedAt
        byte[] RowVersion
    }

    OrderItem {
        int Id PK
        int OrderId FK
        int ProductId FK
        string ProductName
        string SizeName
        decimal UnitPrice
        string Temperature
        string SugarLevel
        string IceLevel
        int Quantity
        string Note
        decimal LineTotal
    }

    OrderItemTopping {
        int Id PK
        int OrderItemId FK
        string ToppingName
        decimal Price
    }

    OrderStatusHistory {
        int Id PK
        int OrderId FK
        int FromStatus
        int ToStatus
        string ChangedBy
        datetime ChangedAt
        string Note
    }

    Promotion {
        int Id PK
        string Code UK
        string Name
        int Type
        decimal Value
        decimal MaxDiscount
        decimal MinOrderValue
        datetime StartAt
        datetime EndAt
        int UsageLimit
        int PerUserLimit
        int UsedCount
        bool IsActive
        bool IsDeleted
    }

    PromotionUsage {
        int Id PK
        int PromotionId FK
        string UserId FK
        int OrderId FK
        datetime UsedAt
    }

    Ingredient {
        int Id PK
        string Code UK
        string Name
        string Unit
        decimal StockQty
        decimal MinThreshold
        bool IsActive
        bool IsDeleted
        byte[] RowVersion
    }

    Recipe {
        int Id PK
        int ProductId FK
        string SizeName
        int IngredientId FK
        decimal Quantity
    }

    StockTransaction {
        int Id PK
        int IngredientId FK
        int Type
        decimal Quantity
        decimal BalanceAfter
        decimal UnitCost
        string Reason
        int OrderId FK
        string CreatedBy
        datetime CreatedAt
    }

    Shift {
        int Id PK
        string StaffId FK
        datetime OpenedAt
        datetime ClosedAt
        decimal OpeningCash
        decimal ExpectedCash
        decimal CountedCash
        decimal Difference
        string Note
    }

    AuditLog {
        int Id PK
        string UserId
        string Action
        string EntityName
        string EntityId
        string OldValues
        string NewValues
        string IpAddress
        datetime CreatedAt
    }

    SystemSetting {
        string Key PK
        string Value
        string Description
        datetime UpdatedAt
    }

    PaymentMethodConfig {
        int Id PK
        string Code UK
        string Name
        bool IsActive
        int DisplayOrder
    }
```

## 2. Ràng buộc toàn vẹn, Index & Concurrency

1. **Unique Indexes:**
   - `Order.Code` (`CF-yyMMdd-NNNN`)
   - `Promotion.Code`
   - `Ingredient.Code`
   - `CafeTable.Code` (tên bảng SQL: `Tables`, lớp C# `CafeTable` để tránh đụng từ khóa `Table` của hệ thống UI)
   - `Category.Name` (lọc `IsDeleted = 0`), `Category.Slug`
   - `SystemSetting.Key`, `PaymentMethodConfig.Code`
2. **Performance Indexes:**
   - `IX_Orders_Status_CreatedAt` trên `Order(Status, CreatedAt)`
   - `IX_Products_CategoryId_IsDeleted` trên `Product(CategoryId, IsDeleted)`
   - `IX_StockTransactions_IngredientId_CreatedAt` trên `StockTransaction(IngredientId, CreatedAt)`
3. **Delete Behavior & Xóa mềm (`IsDeleted`):**
   - Toàn bộ khóa ngoại nghiệp vụ (`Product -> Category`, `Order -> Customer/Staff/Table/Promotion`, `OrderItem -> Product`, `Recipe -> Ingredient`, `StockTransaction -> Ingredient/Order`) cấu hình `DeleteBehavior.Restrict`.
   - Chỉ các bảng con thuần gắn liền vòng đời cha (`OrderItem -> Order`, `OrderItemTopping -> OrderItem`, `OrderStatusHistory -> Order`, `ProductSize -> Product`, `ProductTopping -> Product`) dùng `DeleteBehavior.Cascade` (lưu ý `Order` và `OrderItem` không bao giờ bị xóa trong nghiệp vụ).
   - Hỗ trợ xóa mềm (`IsDeleted = true`) cho: `ApplicationUser`, `Category`, `Product`, `Topping`, `CafeTable`, `Ingredient`, `Promotion`.
4. **Kiểu dữ liệu tiền tệ & Thời gian:**
   - Tiền tệ (`BasePrice`, `Price`, `Subtotal`, `DiscountAmount`, `ShippingFee`, `Total`, `UnitPrice`, `LineTotal`, `Value`, `MaxDiscount`, `MinOrderValue`, `UnitCost`, `OpeningCash`, ...): `decimal(18,0)`.
   - Định lượng nguyên liệu (`StockQty`, `MinThreshold`, `Quantity`, `BalanceAfter`): `decimal(18,2)` để hỗ trợ lẻ gram/ml chính xác.
   - Tọa độ không gian bàn 3D (`PosX`, `PosY`): `double` lưu tọa độ mặt phẳng sàn quán trong cảnh Three.js (đồng bộ giữa Admin 3D Editor, Staff 3D Floor Plan và Customer 3D Table Picker).
   - Thời gian lưu chuẩn `UTC` (`DateTime.UtcNow`), hiển thị ra giao diện theo múi giờ Việt Nam (`UTC+7`, `SE Asia Standard Time`).
5. **Concurrency Tokens (`[Timestamp]` / `rowversion`):**
   - Bật trên `Order.RowVersion`, `CafeTable.RowVersion`, `Ingredient.RowVersion` nhằm chống ghi đè khi nhiều nhân viên cùng thao tác đơn, đổi bàn hoặc trừ tồn kho đồng thời.

