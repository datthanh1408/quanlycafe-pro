# Ma Trận Phân Quyền (RBAC & Claims) – Đạt Mộc Coffee

Hệ thống sử dụng **ASP.NET Core Identity** kết hợp **Role-Based Access Control** (`Admin`, `Staff`, `Customer`) và **Claim-Based Authorization Policy** cho các quyền mở rộng của nhân viên (`◐`). Mọi quyền đều được kiểm tra bắt buộc ở phía **Server** qua `[Authorize]` và Policy Handler.

## 1. Bảng phân quyền chi tiết

Ký hiệu:
- **✔**: Được phép mặc định theo Role
- **–**: Không được phép (trả về `403 Forbidden` thân thiện hoặc redirect đăng nhập nếu chưa xác thực)
- **◐**: Chỉ được phép khi tài khoản `Staff` được `Admin` cấp Claim tương ứng (`claimType = "Permission"`, `claimValue = "<Mã quyền>"`)

| Nhóm | Chức năng | Customer | Staff | Admin | Policy / Claim kiểm tra ở Server |
| --- | --- | :---: | :---: | :---: | --- |
| **Công khai & Mua hàng** | Xem trang chủ 3D, menu, chi tiết món 3D, giới thiệu, liên hệ | ✔ (kể cả khách vãng lai) | ✔ | ✔ | `[AllowAnonymous]` |
| | Giỏ hàng Session, áp mã giảm giá ở giỏ | ✔ (kể cả khách vãng lai) | – | – | Cho phép Anonymous/Customer; chặn Staff/Admin đặt online |
| | Checkout đặt hàng online (chọn bàn 3D / mang đi / giao hàng), xem & theo dõi đơn của mình | ✔ (yêu cầu đăng nhập) | – | – | `[Authorize(Roles = "Customer")]` + kiểm tra `Order.CustomerId == currentUserId` (chống IDOR) |
| **Tài khoản** | Hồ sơ cá nhân, đổi mật khẩu, đăng xuất | ✔ | ✔ | ✔ | `[Authorize]` |
| **Ca & Tổng quan** | Dashboard ca (số đơn, đơn chờ, đơn mới real-time) | – | ✔ | ✔ (Dashboard Admin) | `[Authorize(Roles = "Staff,Admin")]` |
| | Xem doanh thu trong ca tại Staff Dashboard | – | ◐ | ✔ | Policy `CanViewRevenue` (`Admin` hoặc Claim `Staff.ViewRevenue`) |
| | Mở / đóng ca làm việc của mình (P1) | – | ✔ | Xem mọi ca | `[Authorize(Roles = "Staff,Admin")]` |
| **Đơn hàng** | Tiếp nhận, xác nhận, pha chế (trừ kho), sẵn sàng, thu tiền, hoàn thành | – | ✔ | ✔ | `[Authorize(Roles = "Staff,Admin")]` |
| | Hủy đơn hàng | Chỉ đơn `Chờ xác nhận` của chính mình | ◐ (`Chờ xác nhận`, `Đã xác nhận`) | ✔ (mọi trạng thái chưa `Hoàn thành`) | Customer: kiểm tra chủ sở hữu + trạng thái; Staff: Policy `CanCancelOrder` (`Staff.CancelOrder`); Admin: mọi đơn chưa hoàn thành |
| | Hoàn tiền / Void đơn đã `Hoàn thành`, lùi 1 bước trạng thái kèm lý do | – | – | ✔ | `[Authorize(Roles = "Admin")]` |
| **POS & Quầy** | POS bán hàng tại quầy, tạo/giữ đơn mở tại bàn, xem & in hóa đơn | – | ✔ | ✔ | `[Authorize(Roles = "Staff,Admin")]` |
| | Áp dụng mã khuyến mãi tại màn hình POS | – | ◐ | ✔ | Policy `CanApplyPromotion` (`Admin` hoặc Claim `Staff.ApplyPromotion`) |
| **Sơ đồ bàn 3D/2D** | Xem sơ đồ bàn 3D Three.js, mở đơn tại bàn, chuyển bàn, gộp bàn (P1), đổi trạng thái hợp lệ | – | ✔ | ✔ | `[Authorize(Roles = "Staff,Admin")]` |
| | CRUD bàn (thêm, sửa, xóa mềm) & kéo-thả chỉnh tọa độ sơ đồ bàn 3D (`PosX`, `PosY`) | – | – | ✔ | `[Authorize(Roles = "Admin")]` |
| **Khách hàng** | Xem danh sách khách hàng, tìm kiếm, xem lịch sử mua | – | ✔ | ✔ (kèm tổng chi tiêu) | `[Authorize(Roles = "Staff,Admin")]` |
| | Cập nhật thông tin khách hàng | Tự sửa hồ sơ của mình | ◐ | ✔ (kèm khóa/mở khóa) | Policy `CanEditCustomer` (`Admin` hoặc Claim `Staff.EditCustomer`) |
| **Thực đơn** | Bật/tắt nhanh trạng thái "Tạm hết" (`IsAvailable`) của món | – | ◐ | ✔ | Policy `CanToggleProduct` (`Admin` hoặc Claim `Staff.ToggleProduct`) |
| | CRUD Sản phẩm, Size, Công thức, Danh mục, Topping | – | – | ✔ | `[Authorize(Roles = "Admin")]` |
| **Quản trị & Kho** | Quản lý nhân viên, khóa/mở khóa, cấp quyền `◐`, đặt lại mật khẩu | – | – | ✔ | `[Authorize(Roles = "Admin")]` (chặn tự khóa mình / khóa Admin cuối cùng) |
| | Quản lý kho nguyên liệu, nhập/xuất/điều chỉnh kho, khuyến mãi, báo cáo & xuất Excel, cài đặt hệ thống, Audit Log | – | – | ✔ | `[Authorize(Roles = "Admin")]` |

## 2. Danh sách hằng số Permission Claim (`StaffPermissions`)

- `Staff.ViewRevenue`: Xem doanh thu ca trên Staff Dashboard.
- `Staff.CancelOrder`: Hủy đơn hàng ở trạng thái `Chờ xác nhận` hoặc `Đã xác nhận` (bắt buộc nhập lý do).
- `Staff.ApplyPromotion`: Áp dụng mã khuyến mãi trực tiếp trên POS.
- `Staff.EditCustomer`: Chỉnh sửa họ tên, số điện thoại, địa chỉ của khách hàng tại khu Staff.
- `Staff.ToggleProduct`: Bật/tắt nhanh trạng thái còn hàng / tạm hết của món tại `/staff/availability`.

## 3. Tài khoản kiểm thử phân quyền (chỉ tạo ở môi trường `Development`)

| Vai trò | Email | Quyền mở rộng (`◐`) | Đường dẫn đích sau đăng nhập |
| --- | --- | --- | --- |
| **Admin** | `admin@coffee.local` | Toàn quyền hệ thống | `/admin/dashboard` |
| **Staff (đủ quyền ◐)** | `staff@coffee.local` | Có đủ 5 quyền `Staff.*` | `/staff/dashboard` |
| **Staff 2 (cơ bản)** | `staff2@coffee.local` | Không có quyền `Staff.*` nào | `/staff/dashboard` |
| **Customer** | `customer@coffee.local` | Khách hàng mua online | `/` (hoặc `returnUrl` nội bộ hợp lệ) |

