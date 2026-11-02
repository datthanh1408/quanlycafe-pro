# Ghi Nhận Giả Định & Quyết Định Kỹ Thuật (ASSUMPTIONS) – Đạt Mộc Coffee

Tài liệu ghi nhận toàn bộ giá trị mặc định được áp dụng cho các mục để trống `[...]` trong đề bài, thông số môi trường thực tế trên máy, và kiến trúc tích hợp **Three.js 3D** theo yêu cầu của người dùng.

---

## 1. Môi trường & Trạng thái dự án (Giai đoạn 0)

- **Trạng thái dự án:** Dự án mới hoàn toàn (thư mục làm việc ban đầu chưa có `.sln` hay `.csproj`).
- **.NET SDK thực tế trên máy:** `.NET SDK 10.0.401` (`net10.0`), công cụ `dotnet-ef 10.0.8` đã cài sẵn. Hệ thống xây dựng trên **ASP.NET Core MVC (.NET 10)** đúng mặc định ưu tiên của Mục 3.1.
- **Cơ sở dữ liệu:** Microsoft SQL Server LocalDB (`(localdb)\MSSQLLocalDB` v17.0.4025.3 đang hoạt động tốt trên máy). Connection string đặt trong `appsettings.json` / `appsettings.Development.json`, không hard-code trong source code.
- **Quản lý thư viện Frontend cục bộ:** Máy không cài `node`/`npm`, do đó toàn bộ thư viện frontend (**Three.js + OrbitControls**, **Bootstrap 5.3**, **Bootstrap Icons**, **Chart.js**, **SignalR JS Client**) được tải và đóng gói cục bộ trong `wwwroot/lib/` để ứng dụng chạy độc lập 100% không phụ thuộc CDN.

---

## 2. Giá trị mặc định áp dụng cho Thông tin quán (Mục 1)

| Hạng mục | Giá trị áp dụng (lưu vào `SystemSetting` khi Seed) | Ghi chú |
| --- | --- | --- |
| **Tên quán** | `Đạt Mộc Coffee` | Có thể đổi trong `/admin/settings` |
| **Slogan** | `Hương mộc nguyên bản – Trọn vị từng khoảnh khắc` | Slogan đề xuất phù hợp định vị hiện đại, ấm cúng |
| **Loại hình** | `Cafe & Bakery` | 5 danh mục: Cà phê, Trà, Trà sữa, Nước ép & Sinh tố, Bánh ngọt |
| **Địa chỉ** | `128 Nguyễn Huệ, Phường Bến Nghé, Quận 1, TP. Hồ Chí Minh (Dữ liệu mẫu)` | Đánh dấu rõ là dữ liệu mẫu theo yêu cầu |
| **Số điện thoại** | `0900 000 000` | Lưu cấu hình hệ thống |
| **Email** | `contact@example.com` | Lưu cấu hình hệ thống |
| **Giờ hoạt động** | `07:00 – 22:00 hằng ngày` | Hiển thị ở Header/Footer/Liên hệ |
| **Liên kết MXH / Bản đồ** | Để trống mặc định | Giao diện tự động ẩn các nút Facebook / Zalo / Google Maps khi Admin chưa nhập link |
| **Bảng màu thương hiệu** | Chính: `#6F4E37` (Nâu cà phê), Phụ: `#F6EFE7` (Kem latte), Nhấn: `#C8782A` (Caramel) | Xuất ra CSS Variables trong `tokens.css` & override động từ `SettingService` |
| **Đối tượng khách hàng** | Sinh viên và nhân viên văn phòng | Tối ưu đặt món nhanh, tùy chỉnh đường/đá/topping rõ ràng |
| **Thanh toán** | Tiền mặt (bật mặc định) & Chuyển khoản QR (ẩn cho tới khi Admin cấu hình ngân hàng + ảnh QR tại `/admin/settings`) | Đúng quy tắc Mục 1 & Mục 6.3 |
| **Phí giao hàng (P1)** | `15.000 ₫`, miễn phí cho đơn có tạm tính sau giảm từ `150.000 ₫` trở lên | Cấu hình được trong `/admin/settings` |
| **Tiền tệ / Múi giờ / Ngôn ngữ** | `VND` (`1.234.000 ₫`), `UTC+7` (`SE Asia Standard Time`), Tiếng Việt | Lưu DB theo UTC, format hiển thị giờ Việt Nam |

---

## 3. Kiến trúc tích hợp Three.js 3D vào Hệ thống Quản lý Quán Cà Phê

Để đáp ứng trọn vẹn cả đặc tả nghiệp vụ Full-stack ASP.NET Core MVC lẫn yêu cầu **trang web quản lý quán cà phê sử dụng Three.js**, Three.js (WebGL, ES Module cục bộ tại `wwwroot/lib/three/`) được tích hợp trực tiếp vào dữ liệu thật từ SQL Server ở cả 3 phân khu:

1. **Khu Customer – Không gian Quán 3D & Ly Đồ Uống 3D Tương Tác:**
   - **Trang chủ (`/`) & Giới thiệu (`/about`) – `cafe-hero-3d.js`:** Mô hình không gian 3D quán **Đạt Mộc Coffee** (quầy bar espresso, máy pha cà phê, đèn thả trần ấm áp, khu bàn gỗ Tầng 1, Tầng 2, Sân vườn cây xanh, hiệu ứng hạt khói hương cà phê bay lên, chuyển chế độ Ánh sáng Ngày / Đêm, xoay/thu phóng bằng `OrbitControls`, hiển thị số bàn trống thời gian thực từ DB).
   - **Chi tiết món (`/product/{id}`) – `drink-3d.js`:** Mô phỏng 3D trực quan món uống/bánh ngọt theo thời gian thực ngay khi khách bấm chọn tùy chỉnh:
     - Đổi **Size S / M / L** thay đổi kích thước hình học ly 3D mượt mà.
     - Đổi **Nóng / Lạnh**: Món nóng hiển thị cốc sứ/ly giữ nhiệt kèm hạt khói nóng 3D bốc lên; món lạnh hiển thị ly trong suốt vật liệu thủy tinh (`MeshPhysicalMaterial`) kèm các viên đá 3D nổi bên trong với số lượng tỉ lệ đúng mức đá `0% / 30% / 50% / 70% / 100%`.
     - Bật/tắt **Topping** hiển thị trực tiếp lớp trân châu đen/trắng dưới đáy ly, lớp kem cheese/foam phủ trên mặt, thạch trái cây 3D.
   - **Đặt hàng (`/checkout`) – Chọn bàn 3D trực quan:** Khi khách chọn hình thức **Tại quán**, khách có thể chọn bàn trực tiếp trên sơ đồ không gian 3D (chỉ bàn màu xanh lá `Trống` mới cho phép click chọn).

2. **Khu Staff – Sơ Đồ Bàn 3D Thời Gian Thực & Điều Phối POS (`floorplan-3d.js`):**
   - Tại **`/staff/tables`** và tích hợp chọn bàn trên **`/staff/pos`**: Toàn bộ bàn từ bảng `Tables` trong SQL Server được dựng 3D theo 3 khu vực (`Tầng 1`, `Tầng 2`, `Sân vườn`) dựa trên tọa độ `PosX`, `PosY` và sức chứa `Capacity` (tự động xếp số ghế 3D tương ứng 2/4/6/8 ghế quanh bàn).
   - Màu sắc đèn báo & mặt bàn 3D tuân thủ chuẩn Design System: **Trống** (`#10B981`), **Đang sử dụng** (`#F59E0B` – tự động đặt thêm tách cà phê 3D bốc khói trên mặt bàn đang có khách!), **Đã đặt trước** (`#3B82F6` – đặt bảng Reserved 3D trên bàn).
   - Hỗ trợ Raycasting hover/click mở ngay Panel nghiệp vụ (Xem đơn mở hiện tại, Tạo đơn POS tại bàn, Chuyển bàn, Gộp bàn, Đổi trạng thái) và đồng bộ tức thời qua **SignalR** khi có đơn mới hoặc thanh toán xong. Có nút chuyển đổi nhanh chế độ **3D Không gian / 2D Lưới thẻ** cho máy tính bảng cấu hình yếu và tôn trọng `prefers-reduced-motion`.

3. **Khu Admin – Trình Chỉnh Sửa Bố Cục Bàn 3D Kéo-Thả (`/admin/tables`):**
   - Admin không chỉ thêm/sửa/xóa mềm bàn mà còn có chế độ **"Chỉnh sơ đồ 3D (Kéo - Thả)"** trực tiếp trên canvas Three.js: dùng Raycaster kéo trượt bàn trên mặt sàn 3D (có lưới snap-to-grid), đổi góc nhìn camera theo từng khu vực và lưu tọa độ `PosX`, `PosY` về SQL Server qua AJAX có Anti-Forgery Token.

---

## 4. Các giả định nghiệp vụ chi tiết khác

- **Hình ảnh sản phẩm cục bộ:** Tạo bộ ảnh SVG/PNG minh họa chất lượng cao riêng cho từng món và danh mục tại `wwwroot/images/seed/` (ghi đầy đủ trong `docs/IMAGE_CREDITS.md`), đảm bảo hiển thị sắc nét 100% khi không có mạng internet; Admin có thể tải ảnh thật (`jpg/png/webp` ≤ 2 MB, kiểm tra cả phần mở rộng, MIME type và magic bytes) thay thế bất cứ lúc nào.
- **Font chữ:** Khai báo stack font ưu tiên `'Be Vietnam Pro'` và `'Playfair Display'` kèm fallback tiếng Việt hệ thống sắc nét (`Segoe UI`, `system-ui`, `-apple-system`) để không bị lỗi chữ khi offline.
- **Đơn hàng tại quầy (POS) với bàn đang mở:** Khi tạo đơn POS gắn với một bàn, đơn ở trạng thái mở (`Đã xác nhận` / `Đang pha chế` / `Sẵn sàng nhận`), cho phép nhân viên bấm **"Thêm món vào đơn bàn"** cho tới khi khách thanh toán và chuyển sang `Hoàn thành` (lúc đó bàn tự động trả về `Trống`).
- **Tự động trừ kho:** Thực hiện đúng tại thời điểm đơn chuyển sang **`Đang pha chế`** (cả từ luồng xử lý đơn lẫn POS khi bấm chuyển pha chế), bọc trong database transaction. Nếu một món có cả dòng `Recipe` theo `SizeName` cụ thể và dòng `SizeName = null` (áp dụng chung mọi size), hệ thống cộng gộp định lượng nguyên liệu tương ứng để trừ kho chính xác.
- **Chống tạo trùng mã đơn `CF-yyMMdd-NNNN`:** Sinh mã đơn trong transaction có retry/lock an toàn theo ngày `yyMMdd` (tính theo múi giờ Việt Nam UTC+7).

