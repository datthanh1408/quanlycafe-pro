# Đạt Mộc Coffee – Hệ Thống Quản Lý Quán Cà Phê 3D (ASP.NET Core 10 MVC + Three.js)

Hệ thống quản lý quán cà phê và bán hàng đa kênh (**Customer Online – Staff POS – Admin Back-office**) tích hợp **mô phỏng không gian quán 3D, ly đồ uống 3D tương tác và sơ đồ bàn 3D thời gian thực bằng Three.js WebGL**, kết hợp **ASP.NET Core 10 MVC**, **Entity Framework Core**, **SQL Server (LocalDB)**, **SignalR Real-time** và **ClosedXML**.

---

## 1. Điểm Nổi Bật & Tích Hợp Three.js 3D

1. **Khu vực Khách hàng (`Customer` – `_CustomerLayout`):**
   - **Không gian quán 3D tương tác (`/`, `/about`):** Dựng kiến trúc 3D quán **Đạt Mộc Coffee** gồm Quầy Bar pha chế Espresso, Tủ bánh ngọt, Tầng 1, Tầng 2 và Sân vườn cây xanh; hỗ trợ xoay `OrbitControls` 360°, chuyển góc nhìn camera theo khu vực, đổi chế độ ánh sáng **Ngày / Đêm** và hiển thị trạng thái bàn trực tiếp từ SQL Server.
   - **Tùy chỉnh ly đồ uống & bánh ngọt 3D thời gian thực (`/product/{id}`):** Mô hình 3D biến đổi tức thì khi chọn **Size (S/M/L)**, **Pha nóng** (cốc sứ kèm hiệu ứng khói nóng 3D) / **Đá lạnh** (ly trong suốt kèm số lượng viên đá 3D tỉ lệ theo mức đá `0% / 30% / 50% / 70% / 100%`) và **Topping 3D** (trân châu đen/trắng, thạch cà phê, đào miếng, lớp foam kem Macchiato).
   - **Chọn bàn trống trên Sơ đồ 3D (`/checkout`, `/reservation`):** Khách đặt dùng tại quán hoặc đặt bàn trước có thể click chọn trực tiếp bàn trống màu xanh lá trên mặt bằng 3D.
   - Giỏ hàng Session giữ lại trước/sau đăng nhập, kiểm tra mã khuyến mãi thật từ server, theo dõi tiến trình đơn hàng thời gian thực (SignalR), hủy đơn `Chờ xác nhận` và đặt lại đơn cũ.

2. **Khu vực Nhân viên (`Staff` – `_StaffLayout`):**
   - **Sơ đồ bàn 3D thời gian thực (`/staff/tables`):** Màu trạng thái chuẩn (`Trống` `#10B981`, `Đang sử dụng` `#F59E0B` kèm tách cà phê 3D trên bàn, `Đã đặt trước` `#3B82F6`), click mở Panel điều phối (Xem đơn mở, Gọi thêm món vào bàn, Chuyển bàn, Gộp bàn, Đổi trạng thái) đồng bộ tức thì qua SignalR, kèm nút chuyển đổi nhanh chế độ **3D / 2D Lưới thẻ** cho máy tính bảng.
   - **Quầy bán hàng POS chạm nhanh (`/staff/pos`):** Nút thao tác lớn (`≥ 48px`), chọn bàn trên modal 3D, giữ đơn mở cho bàn đang phục vụ, áp mã khuyến mãi (theo quyền `◐`), tính tiền thừa tiền mặt, hiển thị mã QR chuyển khoản, in hóa đơn nhiệt/A5 (`@media print`).
   - **Xử lý đơn & KDS (`/staff/orders`, `/staff/dashboard`):** Nhận chuông & toast thông báo đơn mới qua SignalR (tự fallback polling 15 giây nếu rớt kết nối), chuyển trạng thái từng bước, tự động trừ kho theo công thức khi chuyển sang `Đang pha chế`, mở/đóng ca đối soát tiền mặt.

3. **Khu vực Quản trị viên (`Admin` – `_AdminLayout`):**
   - **Trình chỉnh sửa bố cục bàn 3D Kéo-Thả (`/admin/tables`):** Bật chế độ Kéo-Thả để di chuyển bàn trực tiếp trên sàn 3D Three.js (có lưới snap-to-grid) và lưu tọa độ `(PosX, PosY, Zone)` về SQL Server.
   - **Quản trị toàn diện:** Dashboard KPI & biểu đồ `Chart.js` (doanh thu chỉ tính đơn `Hoàn thành` & `Đã thanh toán`), CRUD Sản phẩm (kèm Size, Topping, Màu chất lỏng 3D, **Công thức nguyên liệu**), Danh mục, Topping, Kho nguyên liệu & lịch sử nhập/xuất/điều chỉnh/hao hụt, Khuyến mãi, Nhân viên & phân quyền chi tiết `◐` (`Staff.*`), Khách hàng, Đơn hàng (kèm lùi 1 bước có lý do & Hoàn tiền/Void), Báo cáo xuất **Excel `.xlsx` (ClosedXML)**, Nhật ký hoạt động (`AuditLog`) và Cài đặt hệ thống.

---

## 2. Cấu Trúc Dự Án (3 Phần Lớn: Database – Backend – Frontend)

Toàn bộ mã nguồn đã được gom nhóm khoa học thành 3 trụ cột độc lập, dễ đọc, dễ mở rộng:

```
d:\testpromt-3d-pro\sexgay\
│
├── Program.cs                                  # Khởi động ứng dụng (Cấu hình DI, DbContext, WebRoot, Razor Engine, SignalR)
├── appsettings.json                            # Cấu hình chuỗi kết nối LocalDB
├── CoffeeShop.csproj                           # Project file ASP.NET Core 10
│
├── 📁 Database/                                # === TẦNG CƠ SỞ DỮ LIỆU & LƯU TRỮ ===
│   ├── Context/AppDbContext.cs                 # EF Core DbContext, cấu hình Fluent API, Index, RowVersion
│   ├── Entities/                               # Toàn bộ thực thể: Identity, Catalog, Order, Inventory, System
│   ├── Migrations/                             # Lịch sử Code-First Migrations
│   └── Seeding/                                # Khởi tạo tài khoản, 22 món, 14 bàn 3D, 60 đơn hàng demo, ảnh SVG
│
├── 📁 Backend/                                 # === TẦNG XỬ LÝ SERVER & NGHIỆP VỤ ===
│   ├── Controllers/                            # Tiếp nhận HTTP Request (Customer, Staff, Admin, Realtime)
│   ├── Services/                               # Nghiệp vụ lõi (Pricing, Order, Inventory, Promo, Table, Shift, Report...)
│   ├── Realtime/OrderHub.cs                    # Kênh truyền thông SignalR Real-time
│   ├── Security/Permissions.cs                 # RBAC (3 Roles: Admin, Staff, Customer & 5 Claims Staff.*)
│   ├── Models/                                 # DTO, Enums & ViewModels trao đổi dữ liệu
│   └── Helpers/                                # Tiện ích định dạng tiền tệ VND, múi giờ VN, upload ảnh
│
├── 📁 Frontend/                                # === TẦNG GIAO DIỆN & TRẢI NGHIỆM 3D ===
│   ├── Views/                                  # Giao diện Razor HTML (.cshtml)
│   │   ├── Home/, Catalog/, CartCheckout/      # Giao diện đặt món & theo dõi đơn của Khách
│   │   ├── CustomerPortal/, Auth/, Error/      # Tài khoản khách, xác thực & trang lỗi
│   │   ├── Areas/Admin/                        # Màn hình quản trị, báo cáo & Kéo-thả bàn 3D
│   │   ├── Areas/Staff/                        # Màn hình Touch POS, Sơ đồ bàn 3D & KDS ca trực
│   │   └── Shared/                             # Layout chung, Toast, Modal, Pagination, Badge
│   └── wwwroot/                                # Tài nguyên tĩnh phục vụ Client (100% Offline)
│       ├── js/                                 # 3 Module Three.js (three-cafe-hero, three-drink-customizer, three-floorplan), pos-app
│       ├── css/                                # tokens.css (màu cà phê, chuẩn nút >= 48px), site.css
│       ├── lib/                                # Three.js v0.170.0, Bootstrap 5.3, Bootstrap Icons, Chart.js, SignalR
│       └── images/seed/                        # 23 tệp ảnh SVG sản phẩm sắc nét cục bộ
│
└── 📁 tests/CoffeeShop.Tests/                  # DỰ ÁN KIỂM THỬ ĐƠN VỊ (xUnit)
    └── CoreBusinessRulesTests.cs               # 4 bài test quy tắc nghiệp vụ cốt lõi
```

---

## 3. Yêu Cầu Môi Trường

- **.NET SDK:** `.NET 10.0` trở lên (đã kiểm thử trên `.NET SDK 10.0.401`).
- **Cơ sở dữ liệu:** Microsoft SQL Server hoặc **SQL Server LocalDB** (`(localdb)\MSSQLLocalDB`).
- **Công cụ EF Core CLI (tùy chọn khi chạy lệnh migration thủ công):** `dotnet-ef` (`10.0.*`).
- **Không cần Node.js hay kết nối Internet khi chạy:** Toàn bộ thư viện Frontend (`Three.js v0.170.0`, `OrbitControls`, `Bootstrap 5.3`, `Bootstrap Icons`, `Chart.js`, `SignalR Client`) đã được đóng gói cục bộ trong `wwwroot/lib/`.

---

## 3. Cấu Hình & Cách Chạy Dự Án

### Bước 1: Kiểm tra Connection String
Mở `appsettings.json` hoặc `appsettings.Development.json` (có thể ghi đè qua User Secrets / biến môi trường `ConnectionStrings__DefaultConnection`):

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=DatMocCoffeeDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
}
```

### Bước 2: Khởi động LocalDB & Cập nhật Database Migration
Mở PowerShell tại thư mục gốc dự án (`d:\testpromt-3d-pro\sexgay`):

```powershell
sqllocaldb start MSSQLLocalDB
dotnet ef database update
```
*(Lưu ý: Khi chạy ứng dụng, `DbInitializer` cũng tự động thực thi `MigrateAsync()` và nạp dữ liệu khởi tạo nếu database đang trống).*

### Bước 3: Build & Chạy ứng dụng

```powershell
dotnet build
dotnet run --urls "http://localhost:5150"
```
Sau đó mở trình duyệt truy cập: **`http://localhost:5150`**

---

## 4. Danh Sách Tài Khoản Test (Môi trường `Development`)

Mật khẩu được cấu hình trong `appsettings.Development.json` (chỉ tự động seed khi chạy ở môi trường `Development`):

| Vai trò | Email đăng nhập | Mật khẩu | Quyền hạn đặc biệt | Đường dẫn mặc định |
| --- | --- | --- | --- | --- |
| **Admin** | `admin@coffee.local` | `Admin@123` | Toàn quyền hệ thống, kéo-thả bàn 3D, kho, báo cáo Excel | `/admin/dashboard` |
| **Staff 1 (Ca trưởng)** | `staff@coffee.local` | `Staff@123` | Có đủ 5 quyền mở rộng `◐` (`ViewRevenue`, `CancelOrder`, `ApplyPromotion`, `EditCustomer`, `ToggleProduct`) | `/staff/dashboard` |
| **Staff 2 (Phục vụ)** | `staff2@coffee.local` | `Staff@123` | Nhân viên cơ bản (không có các quyền `◐`) để kiểm thử phân quyền | `/staff/dashboard` |
| **Customer** | `customer@coffee.local` | `Customer@123` | Đặt hàng online, chọn bàn 3D, tùy chỉnh ly 3D, theo dõi đơn | `/` |

---

## 5. Các Cờ Cấu Hình (`appsettings*.json`)

- `Seed:DemoOrders` (`true` ở `Development`, `false` ở `Production`): Khi bật `true` lúc khởi tạo database mới, hệ thống tự động tạo **60 đơn hàng mẫu** rải đều trong 30 ngày gần nhất **thông qua chính `OrderService`** (trừ kho thật, ghi lịch sử trạng thái thật, có cả đơn hoàn thành, đơn hủy, 2 đơn mở đang ngồi tại bàn `T1-02` & `SV-01` và 2 đơn online chờ xác nhận) để Dashboard, Biểu đồ, Báo cáo Excel và Sơ đồ bàn 3D có sẵn dữ liệu trực quan ngay khi mở.

---

## 6. Tài Liệu Kỹ Thuật Đi Kèm (`docs/`)

- [`docs/ERD.md`](docs/ERD.md): Sơ đồ thực thể quan hệ Mermaid, khóa ngoại, index, kiểu dữ liệu & concurrency token.
- [`docs/PERMISSIONS.md`](docs/PERMISSIONS.md): Ma trận phân quyền chi tiết cho 3 Role & 5 Permission Claims `Staff.*`.
- [`docs/ASSUMPTIONS.md`](docs/ASSUMPTIONS.md): Ghi nhận các giá trị mặc định áp dụng và kiến trúc tích hợp Three.js 3D.
- [`docs/IMAGE_CREDITS.md`](docs/IMAGE_CREDITS.md): Nguồn hình ảnh SVG sản phẩm cục bộ & tài nguyên đồ họa 3D.
- [`docs/TEST_REPORT.md`](docs/TEST_REPORT.md): Báo cáo kiểm thử chức năng, phân quyền, nghiệp vụ và build.

