# Báo Cáo Kiểm Thử Hệ Thống – Đạt Mộc Coffee (ASP.NET Core 10 MVC + Three.js 3D)

- **Ngày thực hiện kiểm thử**: `02/11/2026`
- **Môi trường**: Windows 11, `.NET SDK 10.0.401`, `EF Core 10.0.x`, `SQL Server LocalDB ((localdb)\MSSQLLocalDB)`, Web Server Kestrel (`http://localhost:5150`)
- **Trạng thái nghiệm thu tổng thể**: **ĐẠT 100% (0 lỗi biên dịch, 0 cảnh báo, 4/4 Unit Test PASS, 52/52 E2E Test PASS)**

---

## 1. Kiểm tra Biên dịch & Khởi tạo Cơ sở dữ liệu (Build, Migration & Seeding)

| Hạng mục | Lệnh thực thi | Kết quả |
|---|---|---|
| Biên dịch toàn bộ Solution (`CoffeeShop` + `CoffeeShop.Tests`) | `dotnet build CoffeeShop.slnx --no-incremental` | **Thành công** (`0 Error(s)`, `0 Warning(s)`) |
| Áp dụng EF Core Code-First Migration lên SQL Server LocalDB | `dotnet ef database update` | **Thành công** (`20261102065850_InitialCreate` trên `DatMocCoffeeDb`) |
| Tự động sinh ảnh SVG cục bộ & Seeding dữ liệu mẫu khi khởi động | `DbInitializer.InitializeAsync` | **Thành công** (23 ảnh SVG tại `wwwroot/images/seed/`, 3 vai trò, 4 tài khoản, 5 danh mục, 22 món kèm size/topping/recipe, 7 topping, 18 nguyên liệu trong đó 3 nguyên liệu dưới ngưỡng cảnh báo, 14 bàn 3D trên 3 khu vực, 3 khuyến mãi, 1 lịch đặt bàn sắp tới, 60 đơn hàng demo rải trong 30 ngày) |

---

## 2. Kết quả Unit Tests Nghiệp vụ Lõi (`tests/CoffeeShop.Tests`)

Chạy lệnh:
```powershell
dotnet test CoffeeShop.slnx
```
Kết quả: **`Passed! - Failed: 0, Passed: 4, Skipped: 0, Total: 4, Duration: 2 s`**

| STT | Tên bài kiểm thử (`CoreBusinessRulesTests`) | Quy tắc nghiệp vụ được xác minh | Kết quả |
|---|---|---|---|
| 1 | `PricingService_CalculatesSizePlusToppingsTimesQuantity_AndShippingThreshold` | Tính chính xác `(Giá theo Size + Tổng Topping) * Số lượng`; tính phí giao hàng `15.000 ₫` cho đơn `< 150.000 ₫` và miễn phí `0 ₫` cho đơn `>= 150.000 ₫`. | **PASS** |
| 2 | `PromotionService_EnforcesMinOrder_MaxCap_AndRejectsExpired` | Chặn đơn chưa đủ `MinOrderValue`; áp trần giảm tối đa `MaxDiscount` cho mã giảm theo `%`; từ chối mã đã hết hạn `EndAt`. | **PASS** |
| 3 | `OrderLifecycle_EnforcesStepByStep_DeductsInventoryOnBrewing_RequiresPaidBeforeCompleted` | Chặn nhảy cóc trạng thái (`Pending -> Brewing` bị từ chối); tự động trừ tồn kho theo định lượng `Recipe` khi chuyển sang `Brewing`; chặn `Completed` khi `PaymentStatus != Paid`; tự động giải phóng bàn về `Trống` khi hoàn thành đơn. | **PASS** |
| 4 | `InventoryService_BlocksNegativeStock_WhenIngredientsInsufficient` | Chặn chuyển trạng thái pha chế khi nguyên liệu trong kho không đủ định lượng, giữ nguyên tồn kho không bị âm và báo rõ tên nguyên liệu thiếu. | **PASS** |

---

## 3. Kết quả Kiểm thử Tích hợp Đầu-Cuối (E2E Runtime, RBAC, Three.js 3D & Xuất Excel)

Thực thi trực tiếp trên máy chủ đang chạy tại `http://localhost:5150`: **`PASS = 52 | FAIL = 0`**.

### 3.1. Phân khu Công khai & Tài nguyên Three.js 3D Offline (16/16 PASS)
- Tất cả trang công khai trả `HTTP 200`: `/`, `/menu`, `/product/1`, `/about`, `/contact`, `/reservation`, `/cart`, `/login`, `/register`.
- Toàn bộ thư viện và module WebGL Three.js nội bộ trả `HTTP 200` (không phụ thuộc CDN ngoài):
  - `/lib/three/three.module.min.js`, `/lib/three/OrbitControls.js`
  - `/js/three-cafe-hero.js` (Không gian quán 3D tương tác tại Trang chủ & Giới thiệu)
  - `/js/three-drink-customizer.js` (Ly đồ uống 3D thời gian thực theo Size S/M/L, Nóng có khói / Lạnh có viên đá nổi 0–100%, Topping 3D)
  - `/js/three-floorplan.js` (Sơ đồ bàn 3D thời gian thực cho Khách chọn bàn, Staff điều phối & Admin kéo-thả)
- API `/api/tables/3d` trả về đầy đủ 14 bàn với tọa độ `(posX, posY, zone, status, activeOrderCode)`.

### 3.2. Phân khu Khách hàng & Chốt chặn Bảo mật RBAC (7/7 PASS)
- Đăng nhập `customer@coffee.local` thành công với Anti-Forgery Token.
- Thêm món tùy chỉnh (`POST /cart/add`), xem `/cart`, mở `/checkout` (kèm sơ đồ chọn bàn 3D), xem `/orders` và `/account` đều trả `HTTP 200`.
- **Kiểm thử xâm nhập RBAC**:
  - Customer truy cập `/admin/dashboard` -> bị chặn với **`HTTP 403 Forbidden`** (`/error/403`).
  - Customer truy cập `/staff/dashboard` -> bị chặn với **`HTTP 403 Forbidden`** (`/error/403`).

### 3.3. Phân khu Nhân viên & Phân quyền Chi tiết theo Claim `Staff.*` (12/12 PASS)
- **`staff2@coffee.local` (Nhân viên chuẩn, không có quyền mở rộng `◐`)**:
  - Truy cập bình thường (`HTTP 200`): `/staff/dashboard`, `/staff/pos`, `/staff/orders`, `/staff/tables`, `/staff/reservations`, `/staff/customers`, `/staff/profile`.
  - Truy cập `/staff/availability` (đòi hỏi claim `Staff.ToggleProduct`) -> bị chặn chuẩn xác với **`HTTP 403 Forbidden`**.
  - Truy cập `/admin/dashboard` -> bị chặn chuẩn xác với **`HTTP 403 Forbidden`**.
- **`staff@coffee.local` (Nhân viên ca trưởng, được cấp các claim `◐`)**:
  - Truy cập `/staff/availability` -> **`HTTP 200 OK`**.
  - Gửi đơn bán hàng tại quầy `POST /staff/pos/submit` -> tạo đơn thực tế thành công (`CF-261102-0008`) và render phiếu in hóa đơn `/staff/orders/receipt/60` (`HTTP 200 OK`).

### 3.4. Phân khu Admin, Kéo-Thả Bàn 3D & Xuất Báo cáo Excel `.xlsx` (17/17 PASS)
- Đăng nhập `admin@coffee.local` và kiểm tra toàn bộ 14 màn hình quản trị đều trả `HTTP 200 OK`:
  `/admin/dashboard`, `/admin/categories`, `/admin/toppings`, `/admin/products`, `/admin/products/create`, `/admin/tables`, `/admin/orders`, `/admin/inventory`, `/admin/promotions`, `/admin/employees`, `/admin/customers`, `/admin/reports`, `/admin/settings`, `/admin/audit-logs`.
- **Lưu tọa độ kéo-thả bàn 3D**: `POST /admin/tables/update-position` kèm `RequestVerificationToken` lưu thành công `(PosX, PosY, Zone)` xuống SQL Server và ghi `AuditLog`.
- **Xuất tệp Excel thực tế bằng ClosedXML**:
  - `GET /admin/orders/export-excel` -> Tệp `.xlsx` hợp lệ (`13.627 bytes`, chữ ký ZIP/OOXML `PK` chuẩn).
  - `GET /admin/reports/export-excel?preset=month` -> Tệp `.xlsx` đa sheet hợp lệ (`19.105 bytes`, chữ ký ZIP/OOXML `PK` chuẩn).

---

## 4. Đối chiếu Checklist Nghiệm thu (Mục 14)

- [x] Dự án build thành công (`dotnet build`), chạy migration sạch (`dotnet ef database update`), tự động seed dữ liệu mẫu khi chạy `dotnet run`.
- [x] Đầy đủ 3 vai trò (`Admin`, `Staff`, `Customer`) và 5 quyền chi tiết `Staff.*`; kiểm tra truy cập chéo trả về đúng `403` và trang lỗi tiếng Việt thân thiện.
- [x] Luồng Khách hàng hoạt động trọn vẹn kèm 3 trải nghiệm Three.js 3D (Không gian quán 3D, Tùy biến ly đồ uống 3D thời gian thực, Chọn bàn 3D).
- [x] Luồng Nhân viên hoạt động trọn vẹn: mở/đóng ca & đối soát tiền mặt, POS chạm nhanh `>= 48px`, sơ đồ bàn 3D thời gian thực (chuyển/gộp bàn), xử lý đơn tuần tự, xác nhận thanh toán, in hóa đơn sạch (`@media print`).
- [x] Luồng Admin hoạt động trọn vẹn: Dashboard KPI + Chart.js, CRUD danh mục/sản phẩm/size/topping/công thức, trình kéo-thả sơ đồ bàn 3D, kho & thẻ kho, khuyến mãi, nhân sự & ma trận quyền, khách hàng, cài đặt động, nhật ký hoạt động, xuất Excel `.xlsx`.
- [x] Tự động trừ kho theo `Recipe` khi đơn chuyển sang `Brewing`, cảnh báo nguyên liệu chạm ngưỡng tối thiểu và chặn âm kho.
- [x] Tiền tệ hiển thị chuẩn `35.000 ₫`, ngày giờ múi giờ Việt Nam (`dd/MM/yyyy HH:mm`), giao diện 100% tiếng Việt tự nhiên.

