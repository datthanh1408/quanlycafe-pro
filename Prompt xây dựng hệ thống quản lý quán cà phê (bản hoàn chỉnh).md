# PROMPT: XÂY DỰNG HỆ THỐNG QUẢN LÝ QUÁN CÀ PHÊ (BẢN HOÀN CHỈNH)

> **Cách dùng:** điền các mục `[...]` ở Mục 1 và trạng thái dự án ở Mục 0. Mục nào để trống thì AI áp dụng giá trị "Mặc định" ghi bên cạnh và ghi lại vào `docs/ASSUMPTIONS.md`. Mọi chức năng không gắn nhãn **(P1)** hoặc **(P2)** là **P0 – bắt buộc** (xem Mục 2).

---

## 0. VAI TRÒ VÀ CÁCH LÀM VIỆC

Bạn là Senior Full-stack .NET Developer kiêm Business Analyst và UX/UI Designer. Hãy xây dựng **Coffee Shop Management System** chạy được thật, giao diện chuyên nghiệp, gồm 3 khu vực: **Customer (Khách hàng), Staff (Nhân viên), Admin (Quản trị viên)**.

Quy tắc làm việc:

1. **Làm tự chủ, giao kết quả hoàn chỉnh.** Chỉ hỏi lại khi bị chặn thực sự (thiếu quyền, thiếu môi trường). Điểm chưa rõ → chọn phương án đơn giản, hợp lý nhất cho MVP và ghi vào `docs/ASSUMPTIONS.md`.
2. **Làm theo từng giai đoạn ở Mục 13.** Mỗi giai đoạn: viết code → `dotnet build` → chạy thử → báo cáo. Không sang giai đoạn sau khi build còn lỗi hoặc tính năng của giai đoạn hiện tại chưa chạy đúng.
3. **Báo cáo cuối mỗi giai đoạn** gồm: việc đã làm, file đã tạo/sửa, cách kiểm tra, tài khoản test, vấn đề còn tồn.
4. **Trạng thái dự án: \[DỰ ÁN CÓ SẴN / DỰ ÁN MỚI\]** (Mặc định: nếu thư mục làm việc đã có file `.sln` hoặc `.csproj` thì là có sẵn).
   - *Có sẵn:* đọc cấu trúc hiện tại, giữ nguyên framework, quy ước đặt tên, cơ chế xác thực và database đang dùng; chỉ bổ sung hoặc sửa phần cần thiết, không viết lại từ đầu.
   - *Mới:* tạo theo Mục 3.
5. **Không báo "hoàn thành"** nếu chưa build và chạy thử thực tế.

---

## 1. THÔNG TIN QUÁN CÀ PHÊ

| Mục | Giá trị | Mặc định nếu để trống |
| --- | --- | --- |
| Tên quán | \[TÊN QUÁN\] | Đạt Mộc Coffee |
| Slogan | \[SLOGAN\] | AI đề xuất 1 slogan phù hợp |
| Loại hình | \[CAFE / TRÀ SỮA / COFFEE & BAKERY / KHÁC\] | Cafe & Bakery |
| Địa chỉ | \[ĐỊA CHỈ\] | Địa chỉ mẫu tại Việt Nam, đánh dấu rõ là dữ liệu mẫu |
| Số điện thoại | \[SỐ ĐIỆN THOẠI\] | 0900 000 000 |
| Email | \[EMAIL\] | contact@example.com |
| Giờ hoạt động | \[GIỜ MỞ CỬA\] | 07:00 – 22:00 hằng ngày |
| Facebook / Zalo / Google Maps | \[LIÊN KẾT\] | Để trống; tự ẩn mục khi chưa có link |
| Màu thương hiệu chính | \[PRIMARY\_COLOR\] | `#6F4E37` (nâu cà phê) |
| Màu phụ | \[SECONDARY\_COLOR\] | `#F6EFE7` (kem) |
| Màu nhấn | \[ACCENT\_COLOR\] | `#C8782A` (caramel) |
| Đối tượng khách hàng | \[SINH VIÊN / NHÂN VIÊN VĂN PHÒNG / GIA ĐÌNH / KHÁC\] | Sinh viên và nhân viên văn phòng |
| Website tham khảo | \[LINK THAM KHẢO\] | Không có |
| Phương thức thanh toán | \[TIỀN MẶT / CHUYỂN KHOẢN QR / THẺ\] | Tiền mặt và chuyển khoản QR |
| Thông tin chuyển khoản | \[NGÂN HÀNG – CHỦ TÀI KHOẢN – ẢNH QR\] | Ẩn phương thức chuyển khoản cho đến khi Admin cấu hình |
| Phí giao hàng (P1) | \[PHÍ – NGƯỠNG MIỄN PHÍ\] | 15.000 ₫, miễn phí cho đơn từ 150.000 ₫ |
| Tiền tệ, múi giờ, ngôn ngữ | VND, giờ Việt Nam (UTC+7), tiếng Việt | Cố định |

**Định vị thương hiệu:** hiện đại, thân thiện, chuyên nghiệp, tạo cảm giác thoải mái và đáng tin cậy.

---

## 2. MỤC TIÊU, NGUYÊN TẮC THIẾT KẾ VÀ PHẠM VI ƯU TIÊN

Hệ thống giúp quán quản lý toàn bộ hoạt động kinh doanh trên một nền tảng duy nhất.

**Yêu cầu chung**

- Giao diện chuyên nghiệp, thẩm mỹ cao, trực quan, dễ dùng. Không dùng template dashboard đại trà hoặc giao diện sơ sài.
- Tách rõ 3 khu vực Customer, Staff, Admin: mỗi khu vực có layout, menu và quyền riêng.
- Hình ảnh sản phẩm chất lượng cao (nguồn ảnh ở Mục 11); animation, hover và chuyển cảnh nhẹ nhàng.
- Đủ trạng thái loading, empty, success, error ở mọi màn hình có dữ liệu hoặc thao tác.
- Responsive desktop, tablet, mobile (Mục 12).
- Ưu tiên trải nghiệm người dùng và tính thực tế của nghiệp vụ.

**Độ ưu tiên (thay cho các cụm "nếu có", "nếu được hỗ trợ" mơ hồ)**

| Mức | Ý nghĩa | Nội dung |
| --- | --- | --- |
| **P0** | Bắt buộc, hoàn thành và kiểm thử trước | Mọi chức năng không gắn nhãn; gồm xuất báo cáo Excel, trừ kho tự động theo công thức, thông báo đơn mới bằng SignalR cho Staff |
| **P1** | Nên có, làm sau khi P0 ổn định | Giao hàng; đặt bàn trước; mở/đóng ca và đối soát tiền; lịch ca làm; tích điểm; quên mật khẩu qua email; nhật ký hoạt động (audit log); in hóa đơn; gộp bàn; xuất PDF; unit test; cập nhật real-time cho Customer |
| **P2** | Mở rộng, chỉ làm khi còn thời gian | Đánh giá món; hạng thành viên; tiếng Anh; dark mode |

---

## 3. CÔNG NGHỆ VÀ KIẾN TRÚC

### 3.1. Công nghệ

- **Backend:** ASP.NET Core MVC. Phiên bản theo SDK đang cài trên máy: chạy `dotnet --version` ở Giai đoạn 0; mặc định .NET 10, nếu máy chưa có thì dùng bản LTS mới nhất đã cài và ghi rõ vào `docs/ASSUMPTIONS.md`. Không tự nâng/hạ framework của dự án có sẵn.
- **ORM và database:** Entity Framework Core + Microsoft SQL Server (LocalDB hoặc SQL Express), Code-First với migration.
- **Xác thực:** ASP.NET Core Identity với 3 role Admin, Staff, Customer. Nếu dự án có sẵn đang dùng Cookie Authentication thì giữ nguyên và bổ sung phân quyền tương ứng.
- **Real-time:** SignalR, tự động fallback polling 15 giây khi mất kết nối.
- **Frontend:** Razor Views, HTML5, CSS3, JavaScript (ES6), Bootstrap 5.3, Bootstrap Icons, Chart.js. Thư viện cài cục bộ (LibMan hoặc npm), chạy được khi không có CDN.
- **Font:** Be Vietnam Pro (nội dung, hỗ trợ tiếng Việt đầy đủ), Playfair Display (tiêu đề khu Customer).
- **Xuất báo cáo:** ClosedXML cho Excel (P0); QuestPDF hoặc trang in CSS cho PDF (P1).
- **Test (P1):** xUnit.
- **Không** tích hợp cổng thanh toán hoặc dịch vụ bên thứ ba khi chưa được yêu cầu.

### 3.2. Cấu trúc thư mục đề xuất (dự án mới)

```
CoffeeShop/
├─ Areas/
│  ├─ Admin/            # Controllers, Views, ViewModels
│  └─ Staff/            # Controllers, Views, ViewModels
├─ Controllers/         # Khu Customer
├─ Data/                # AppDbContext, Configurations, Migrations, Seed
├─ Models/Entities/     # Entity
├─ Models/ViewModels/   # ViewModel dùng chung
├─ Services/            # Interface + triển khai nghiệp vụ
├─ Hubs/                # SignalR
├─ Authorization/       # Permission, Policy, Handler
├─ Views/Shared/        # Layout, partial, ViewComponent dùng chung
├─ wwwroot/             # css, js, lib, images, uploads
└─ docs/                # ERD, ASSUMPTIONS, PERMISSIONS, TEST_REPORT, IMAGE_CREDITS
```

### 3.3. Nguyên tắc kiến trúc

- Controller mỏng; logic nghiệp vụ nằm trong Services, đăng ký qua Dependency Injection.
- Dùng ViewModel/DTO, không đưa Entity trực tiếp ra View; chống over-posting khi model binding.
- Các service bắt buộc: `PricingService` (nơi duy nhất tính giá), `CartService`, `OrderService` (quản lý chuyển trạng thái), `PromotionService`, `InventoryService`, `TableService`, `ReportService`, `SettingService`; thêm `ShiftService` và `AuditService` khi làm P1.
- Giỏ hàng lưu Session và giữ lại sau khi đăng nhập. Khách chưa đăng nhập được xem menu và thêm giỏ; phải đăng nhập để đặt hàng.
- Dùng transaction cho: tạo đơn, đổi trạng thái có trừ kho, thanh toán, chuyển/gộp bàn. Dùng concurrency token (`RowVersion`) cho Order, Table, Ingredient.
- **Xóa mềm** (`IsDeleted`) cho Product, Category, Topping, Table, Ingredient, Promotion và tài khoản người dùng. Không xóa cứng dữ liệu đã có đơn hàng liên quan. Order và OrderItem không bao giờ bị xóa.
- Tiền lưu `decimal(18,0)`, hiển thị dạng `1.234.000 ₫`. Thời gian lưu UTC, hiển thị giờ Việt Nam.
- Phân trang, tìm kiếm, lọc, sắp xếp thực hiện ở server (truy vấn EF), không tải toàn bộ bảng về client.
- Ghi log bằng `ILogger`; xử lý lỗi toàn cục; trang lỗi tùy chỉnh 403, 404, 500 thân thiện, không lộ stack trace.
- Cấu hình qua `appsettings*.json`, User Secrets hoặc biến môi trường. Không hard-code connection string, mật khẩu, khóa bí mật.

---

## 4. MÔ HÌNH DỮ LIỆU

Thiết kế database có quan hệ hợp lý, quản lý bằng migration. Vẽ ERD (Mermaid) tại `docs/ERD.md` **trước khi** viết code ở Giai đoạn 1.

### 4.1. Các entity chính

| Entity | Trường chính |
| --- | --- |
| ApplicationUser (kế thừa IdentityUser) | FullName, Phone, Address, AvatarUrl, IsActive, LoyaltyPoints (P1), CreatedAt |
| EmployeeProfile | UserId, Position, HireDate, WorkStatus (Đang làm / Nghỉ phép / Đã nghỉ) |
| Category | Name, Slug, ImageUrl hoặc IconClass, DisplayOrder, IsActive, IsDeleted |
| Product | CategoryId, Name, Description, ImageUrl, BasePrice, AllowTemperature, AllowSugar, AllowIce, IsAvailable, IsFeatured, SoldCount, IsDeleted |
| ProductSize | ProductId, SizeName (S/M/L), Price, DisplayOrder |
| Topping, ProductTopping | Name, Price, IsActive; quan hệ N-N với Product |
| Table | Code, Zone, Capacity, Status (Trống / Đang sử dụng / Đã đặt trước), PosX, PosY, IsActive, RowVersion |
| TableReservation (P1) | TableId, CustomerName, Phone, PartySize, ReservedAt, Status, Note |
| Order | Code, CustomerId?, StaffId?, TableId?, OrderType (Tại quán / Mang đi / Giao hàng), Source (Online / POS), Status, PaymentMethod, PaymentStatus (Chưa thanh toán / Đã thanh toán / Đã hoàn tiền), Subtotal, DiscountAmount, ShippingFee, Total, PromotionId?, CustomerName, Phone, DeliveryAddress, Note, CancelReason, ShiftId?, CreatedAt, UpdatedAt, RowVersion |
| OrderItem | OrderId, ProductId, ProductName (snapshot), SizeName, UnitPrice (snapshot), Temperature, SugarLevel, IceLevel, Quantity, Note, LineTotal |
| OrderItemTopping | OrderItemId, ToppingName (snapshot), Price (snapshot) |
| OrderStatusHistory | OrderId, FromStatus, ToStatus, ChangedBy, ChangedAt, Note |
| Promotion, PromotionUsage | Code, Name, Type (Phần trăm / Số tiền), Value, MaxDiscount, MinOrderValue, StartAt, EndAt, UsageLimit, PerUserLimit, UsedCount, IsActive; PromotionId, UserId, OrderId |
| Ingredient | Code, Name, Unit, StockQty, MinMinwshold, IsActive, IsDeleted, RowVersion |
| Recipe | ProductId, SizeName (null = mọi size), IngredientId, Quantity |
| StockTransaction | IngredientId, Type (Nhập / Xuất / Điều chỉnh / Bán hàng / Hao hụt), Quantity, BalanceAfter, UnitCost, Reason, OrderId?, CreatedBy, CreatedAt |
| Shift, WorkSchedule (P1) | StaffId, OpenedAt, ClosedAt, OpeningCash, ExpectedCash, CountedCash, Difference, Note; StaffId, Date, ShiftType |
| AuditLog (P1) | UserId, Action, EntityName, EntityId, OldValues, NewValues, IpAddress, CreatedAt |
| SystemSetting | Key, Value (thông tin quán, màu, giờ mở cửa, thông tin chuyển khoản, phí giao hàng, quy tắc tích điểm...) |
| PaymentMethod | Code, Name, IsActive, DisplayOrder |
| Review (P2) | ProductId, UserId, OrderId, Rating, Comment, CreatedAt |

**Index và quan hệ:** unique cho `Order.Code`, `Promotion.Code`, `Ingredient.Code`, `Table.Code`; index cho `Order(Status, CreatedAt)` và `Product(CategoryId, IsDeleted)`; mọi khóa ngoại dùng `DeleteBehavior.Restrict` trừ các bảng con thuần (OrderItem, OrderItemTopping, ProductSize).

### 4.2. Ràng buộc và validation (client và server)

- Giá bán > 0; phụ thu ≥ 0; số lượng đặt từ 1 đến 99; tồn kho ≥ 0.
- Số điện thoại Việt Nam hợp lệ: `^(0|\+84)(3|5|7|8|9)\d{8}$`.
- Email hợp lệ và duy nhất; mật khẩu tối thiểu 8 ký tự gồm chữ hoa, chữ thường và số.
- Mã giảm giá duy nhất, chỉ gồm chữ, số, gạch ngang; phần trăm giảm từ 1 đến 100; ngày kết thúc sau ngày bắt đầu.
- Tên danh mục, mã bàn, mã nguyên liệu không trùng.
- Ảnh tải lên: jpg, png hoặc webp, tối đa 2 MB.
- Ghi chú tối đa 200 ký tự; lý do hủy đơn là bắt buộc.

---

## 5. HỆ THỐNG ĐĂNG NHẬP VÀ PHÂN QUYỀN

### 5.1. Chức năng xác thực

- Đăng nhập, đăng ký (chỉ tạo tài khoản Customer), đăng xuất, đổi mật khẩu.
- Quên và đặt lại mật khẩu qua email **(P1)** bằng `IEmailSender`; môi trường dev ghi liên kết đặt lại ra console/log thay vì gửi mail thật.
- Sau khi đăng nhập tự chuyển đến giao diện tương ứng: Admin → `/admin/dashboard`; Staff → `/staff/dashboard`; Customer → trang chủ hoặc `returnUrl` hợp lệ (chống open redirect).
- Tài khoản bị khóa hoặc ngừng hoạt động không đăng nhập được và nhận thông báo rõ ràng. Khóa tạm 5 phút sau 5 lần nhập sai mật khẩu.
- Staff do Admin tạo; Customer tự đăng ký. Admin không thể khóa hoặc xóa chính mình và không thể khóa/xóa Admin cuối cùng.

### 5.2. Ma trận phân quyền

Ký hiệu: **✔** được phép; **–** không được phép; **◐** chỉ khi Admin cấp quyền riêng cho nhân viên (lưu bằng claim, cấu hình tại `/admin/settings` → Vai trò & quyền).

| Chức năng | Customer | Staff | Admin |
| --- | --- | --- | --- |
| Xem menu, chi tiết món, giới thiệu, liên hệ | ✔ (kể cả chưa đăng nhập) | ✔ | ✔ |
| Giỏ hàng, đặt hàng online, theo dõi đơn của mình | ✔ | – | – |
| Hồ sơ cá nhân, đổi mật khẩu | ✔ | ✔ | ✔ |
| Dashboard ca (số đơn, đơn chờ, đơn mới) | – | ✔ | ✔ (dashboard Admin) |
| Xem doanh thu ca | – | ◐ `Staff.ViewRevenue` | ✔ |
| Tiếp nhận, xác nhận, chuyển trạng thái đơn | – | ✔ | ✔ |
| Hủy đơn | Chỉ đơn "Chờ xác nhận" của mình | ◐ `Staff.CancelOrder` (trước "Đang pha chế") | ✔ (mọi trạng thái chưa hoàn thành) |
| Hoàn tiền, void đơn đã hoàn thành | – | – | ✔ |
| POS bán tại quầy, xem/in hóa đơn | – | ✔ | ✔ |
| Áp dụng khuyến mãi tại POS | – | ◐ `Staff.ApplyPromotion` | ✔ |
| Bàn: xem sơ đồ, tạo đơn tại bàn, chuyển/gộp bàn, đổi trạng thái | – | ✔ | ✔ |
| Bàn: thêm, sửa, xóa bàn, chỉnh sơ đồ | – | – | ✔ |
| Khách hàng: xem danh sách, tìm kiếm, xem lịch sử mua | – | ✔ | ✔ (kèm tổng chi tiêu) |
| Khách hàng: cập nhật thông tin | Tự cập nhật hồ sơ | ◐ `Staff.EditCustomer` | ✔ |
| Bật/tắt trạng thái "tạm hết" của món | – | ◐ `Staff.ToggleProduct` | ✔ |
| Mở/đóng ca của mình (P1) | – | ✔ | Xem mọi ca |
| Quản lý sản phẩm, danh mục, topping | – | – | ✔ |
| Quản lý nhân viên, phân quyền, lịch ca | – | – | ✔ |
| Kho, khuyến mãi, báo cáo, nhật ký, cài đặt | – | – | ✔ |

Lập bảng này thành `docs/PERMISSIONS.md` và dùng làm căn cứ viết test phân quyền.

### 5.3. Quy tắc kỹ thuật

- Bảo vệ ở **phía server** bằng `[Authorize(Roles = ...)]` và policy theo permission cho từng Area, Controller, Action. Ẩn menu không thay thế cho việc kiểm tra quyền.
- Chưa đăng nhập → chuyển đến trang đăng nhập. Đã đăng nhập nhưng thiếu quyền → trang 403 thân thiện kèm thông báo "Bạn không có quyền truy cập chức năng này".
- Kiểm tra quyền sở hữu dữ liệu: Customer chỉ xem và hủy được đơn của chính mình (chống IDOR).
- Mọi hành động nhạy cảm (đổi giá, hủy đơn, hoàn tiền, khóa tài khoản, đổi quyền, điều chỉnh kho) ghi vào AuditLog **(P1)**.

---

## 6. QUY TẮC NGHIỆP VỤ CỐT LÕI

### 6.1. Trạng thái đơn hàng

Chờ xác nhận → Đã xác nhận → Đang pha chế → Sẵn sàng nhận → Hoàn thành; nhánh phụ: **Đã hủy**. Đơn Giao hàng **(P1)** có thêm "Đang giao" giữa "Sẵn sàng nhận" và "Hoàn thành".

- Chỉ được chuyển tiến từng bước, không nhảy bước. Admin được lùi một bước kèm lý do.
- Đơn tạo từ POS bắt đầu ở trạng thái "Đã xác nhận".
- Mọi lần đổi trạng thái ghi vào `OrderStatusHistory` và đẩy cập nhật qua SignalR.
- Mã đơn có dạng `CF-yyMMdd-NNNN`, đánh số lại theo ngày, duy nhất, sinh an toàn khi nhiều đơn tạo đồng thời.

### 6.2. Giá và tổng tiền

- Thành tiền dòng = (giá theo size đã chọn + tổng giá topping) × số lượng.
- Tạm tính = tổng các dòng. **Tổng tiền = Tạm tính − Giảm giá + Phí giao hàng.**
- Server luôn tính lại giá từ database, không tin giá do client gửi lên. Tên món, giá, topping được snapshot vào OrderItem lúc đặt nên đổi giá sau này không ảnh hưởng đơn cũ.
- Món `IsAvailable = false` hoặc bị ẩn không thể thêm vào giỏ hay đặt; nếu giỏ đang chứa món đó thì báo rõ ở giỏ hàng và checkout.

### 6.3. Thanh toán

- **Tiền mặt:** thu khi nhận hàng hoặc tại quầy; nhân viên bấm "Xác nhận đã thu tiền".
- **Chuyển khoản QR:** hiển thị ảnh QR do Admin tải lên ở Cài đặt, kèm số tiền và nội dung chuyển khoản là mã đơn. Nhân viên hoặc Admin đối chiếu rồi bấm "Xác nhận đã nhận tiền". Không tích hợp cổng thanh toán, không gọi dịch vụ bên ngoài, không tự động đối soát.
- **Thẻ qua máy POS (P1):** ghi nhận thủ công.
- Đơn chỉ được chuyển sang "Hoàn thành" khi `PaymentStatus = Đã thanh toán`.

### 6.4. Hủy đơn và hoàn tiền

- Customer hủy được khi đơn ở "Chờ xác nhận". Staff có quyền `CancelOrder` hủy được từ "Chờ xác nhận" và "Đã xác nhận". Từ "Đang pha chế" trở đi chỉ Admin được hủy. Đơn "Hoàn thành" không hủy được; chỉ Admin hoàn tiền (void) kèm lý do.
- Bắt buộc nhập lý do; lưu người hủy và thời điểm hủy.
- Đơn đã thanh toán trước khi hủy → `PaymentStatus = Đã hoàn tiền` (ghi nhận thủ công, lưu người thực hiện). Đồng thời hoàn lại lượt dùng mã giảm giá và trả bàn về "Trống".
- Kho: nguyên liệu đã bị trừ từ lúc "Đang pha chế" nên **không tự hoàn kho** khi hủy; Admin điều chỉnh thủ công bằng giao dịch loại "Hao hụt" hoặc "Điều chỉnh".

### 6.5. Khuyến mãi

- Mã hợp lệ khi: đang hoạt động, trong khoảng thời gian áp dụng, đạt giá trị đơn tối thiểu, còn lượt sử dụng, người dùng chưa vượt giới hạn.
- Mỗi đơn chỉ dùng 1 mã. Giảm theo phần trăm có mức giảm tối đa. Số tiền giảm không vượt quá tạm tính và không áp lên phí giao hàng.
- Chỉ tăng `UsedCount` khi đơn được tạo thành công (trong cùng transaction). Mã không hợp lệ phải báo rõ lý do.

### 6.6. Kho và công thức

- Mỗi món (theo size) có công thức định lượng nguyên liệu (`Recipe`). Topping cũng có thể có công thức **(P1)**.
- Khi đơn chuyển sang "Đang pha chế": tự động trừ kho theo công thức × số lượng và tạo `StockTransaction` loại "Bán hàng" gắn `OrderId`, trong cùng transaction.
- **Không cho tồn kho âm.** Nếu thiếu nguyên liệu: chặn chuyển trạng thái và hiển thị rõ nguyên liệu nào thiếu; Admin xử lý bằng nhập kho hoặc điều chỉnh.
- Món chưa có công thức thì không trừ kho và hiển thị cảnh báo ở trang quản lý sản phẩm.
- Tồn ≤ ngưỡng cảnh báo → hiện cảnh báo ở dashboard Admin và trang kho.
- Nhập, xuất, điều chỉnh kho bắt buộc có lý do. Lịch sử kho chỉ thêm, không sửa hoặc xóa.

### 6.7. Bàn

- Tạo đơn tại bàn → bàn "Đang sử dụng". Đơn "Hoàn thành" hoặc "Đã hủy" → bàn "Trống". Mỗi bàn chỉ có tối đa 1 đơn đang mở.
- Đơn tại bàn là **đơn mở**: được thêm món cho đến khi thanh toán xong.
- Chuyển bàn: chuyển đơn mở sang bàn trống, cập nhật cả hai bàn trong 1 transaction. Gộp bàn **(P1)**: gộp đơn mở của 2 bàn thành 1 đơn.
- "Đã đặt trước" **(P1)**: bàn có lịch đặt trong 30 phút tới (cấu hình được) hiển thị trạng thái đặt trước.
- Chặn các thay đổi trạng thái bàn trái logic (ví dụ đặt "Trống" khi bàn còn đơn mở).

### 6.8. Ca làm việc (P1)

- Nhân viên mở ca và nhập tiền đầu ca; đơn xử lý trong ca gắn `ShiftId`.
- Đóng ca: hệ thống tính tiền mặt dự kiến (đầu ca + tiền mặt đã thu), nhân viên nhập tiền thực đếm, hiển thị chênh lệch và báo cáo ca; cảnh báo nếu còn đơn mở chưa xử lý.
- Khi chưa làm P1: "trong ca" được hiểu là trong ngày hiện tại.

### 6.9. Tích điểm (P1)

Mặc định 1 điểm cho mỗi 10.000 ₫, 1 điểm = 100 ₫ khi đổi giảm giá; cấu hình trong Cài đặt. Cộng điểm khi đơn "Hoàn thành", trừ điểm khi hoàn tiền.

### 6.10. Real-time và xung đột dữ liệu

- Đơn mới → thông báo ngay cho nhóm Staff (toast, cập nhật badge, âm thanh **(P1)**). Đổi trạng thái → cập nhật cho Staff và Customer đang xem đơn **(P1)**.
- SignalR mất kết nối → tự chuyển sang polling 15 giây.
- Hai người cùng sửa một đơn: dùng `RowVersion`, báo "Đơn vừa được cập nhật bởi người khác" và tải lại dữ liệu.

---

## 7. CHỨC NĂNG CHI TIẾT THEO KHU VỰC

Mọi dữ liệu hiển thị phải lấy từ database qua EF Core, không dùng mảng dữ liệu giả trong View hay JavaScript.

### 7.1. Khu vực Customer (`_CustomerLayout`)

Mục tiêu: giúp khách xem thực đơn, chọn món, đặt hàng và theo dõi đơn dễ dàng, nhất là trên điện thoại.

- **`/` Trang chủ:** header (logo, tên quán, Trang chủ, Thực đơn, Giới thiệu, Liên hệ, Giỏ hàng kèm số lượng, Đăng nhập hoặc Tài khoản); hero lớn có slogan và nút "Khám phá menu"; món nổi bật (món `IsFeatured` hoặc bán chạy thật từ database); danh mục sản phẩm; giới thiệu quán và câu chuyện thương hiệu; ưu đãi đang hoạt động (lấy từ Promotion); gallery không gian quán; footer (địa chỉ, liên hệ, mạng xã hội, giờ mở cửa).
- **`/menu` Thực đơn:** toàn bộ sản phẩm, chip danh mục, tìm kiếm (debounce), lọc theo giá, danh mục, trạng thái còn hàng; sắp xếp theo giá tăng/giảm, phổ biến, mới nhất; phân trang hoặc "Xem thêm". Thẻ món gồm ảnh, tên, mô tả ngắn, giá, nút Xem chi tiết và Thêm vào giỏ. Món tạm hết hiển thị mờ kèm nhãn "Tạm hết" và không thể đặt.
- **`/product/{id}` Chi tiết:** ảnh, tên, mô tả; chọn size S/M/L (nếu món có), nóng/lạnh, mức đường, mức đá (0/30/50/70/100%), topping (nhiều lựa chọn), số lượng, ghi chú; giá cập nhật tức thì theo lựa chọn; validate lựa chọn bắt buộc; gợi ý món cùng danh mục; nút Thêm vào giỏ.
- **`/cart` Giỏ hàng:** danh sách món (ảnh, tên, size, topping, tùy chọn, đơn giá, thành tiền); tăng/giảm số lượng (cập nhật AJAX), xóa món; nhập mã giảm giá (kiểm tra thật qua `PromotionService`); tạm tính, giảm giá, tổng tiền; nút Tiếp tục mua hàng và Tiến hành đặt hàng; empty state khi giỏ trống.
- **`/checkout` Đặt hàng:** yêu cầu đăng nhập; thông tin khách (tên, số điện thoại, tự điền từ hồ sơ); hình thức nhận: **Tại quán** (chọn bàn trống, tùy chọn), **Mang đi**, **Giao hàng (P1)** (địa chỉ, phí theo cấu hình); phương thức thanh toán theo cấu hình; mã giảm giá; ghi chú; tóm tắt đơn; nút Xác nhận. Server kiểm tra lại toàn bộ, chống bấm đặt hai lần. Sau khi đặt: trang "Đặt hàng thành công" kèm mã đơn, hướng dẫn chuyển khoản (ảnh QR, nội dung là mã đơn) nếu chọn chuyển khoản, và nút Theo dõi đơn.
- **`/orders` Theo dõi đơn:** danh sách đơn của tôi (mã, ngày đặt, tổng tiền, badge trạng thái), lọc theo trạng thái, phân trang. **`/orders/{code}`:** chi tiết đơn, timeline tiến trình (Chờ xác nhận → Đã xác nhận → Đang pha chế → Sẵn sàng nhận → Hoàn thành hoặc Đã hủy), cập nhật real-time **(P1)**, nút Hủy khi đơn còn "Chờ xác nhận", nút Đặt lại **(P1)**, đánh giá món **(P2)**.
- **`/account` Tài khoản:** thông tin cá nhân, cập nhật hồ sơ và ảnh đại diện, lịch sử mua hàng, điểm tích lũy **(P1)**, sổ địa chỉ **(P1)**, đổi mật khẩu, đăng xuất.
- **`/about`, `/contact`:** giới thiệu thương hiệu, câu chuyện quán, ảnh không gian, địa chỉ, giờ hoạt động, số điện thoại, nhúng Google Maps từ liên kết cấu hình, kênh hỗ trợ (Zalo, Facebook).
- **`/login`, `/register`, `/forgot-password` (P1):** form có validate, thông báo lỗi và thành công rõ ràng.
- **`/reservation` Đặt bàn (P1):** chọn ngày giờ, số người, tên, số điện thoại, ghi chú; tạo `TableReservation` ở trạng thái chờ xác nhận để Staff hoặc Admin duyệt.

### 7.2. Khu vực Staff (`_StaffLayout`)

Mục tiêu: xử lý đơn và phục vụ nhanh. Giao diện tối giản, nút thao tác lớn (tối thiểu 48 px), tối ưu cho máy tính bảng, trạng thái đơn phân biệt bằng màu (Mục 9).

- **`/staff/dashboard`:** tổng số đơn trong ca, đơn chờ, đơn đang xử lý, đơn hoàn thành; doanh thu ca (◐ `ViewRevenue`); đơn mới nhất; thông báo đơn mới real-time; nút mở/đóng ca **(P1)**.
- **`/staff/orders`:** tất cả đơn với tab theo trạng thái; tìm theo mã đơn; lọc theo ngày và trạng thái; xem chi tiết (món, tùy chọn, ghi chú, thanh toán); các nút hành động theo trạng thái hiện tại (Xác nhận → Bắt đầu pha chế → Sẵn sàng → Hoàn thành); xác nhận thu tiền/nhận chuyển khoản; hủy đơn (◐ `CancelOrder`, bắt buộc lý do); in hóa đơn **(P1)** bằng trang in CSS `@media print`.
- **`/staff/tables`:** sơ đồ bàn trực quan theo khu vực, màu theo trạng thái (Trống, Đang sử dụng, Đã đặt trước), hiển thị mã bàn và sức chứa. Bấm vào bàn mở panel: đơn hiện tại, Tạo đơn tại bàn, Chuyển bàn, Gộp bàn **(P1)**, cập nhật trạng thái theo quy tắc 6.7, xem lịch đặt trước **(P1)**.
- **`/staff/pos`:** bố cục 2 cột. Trái: danh mục, ô tìm kiếm, lưới món nút lớn. Phải: đơn hiện tại. Modal chọn size, nóng/lạnh, đường, đá, topping; chỉnh số lượng, xóa món; chọn bàn hoặc mang đi; áp dụng khuyến mãi (◐ `ApplyPromotion`); tổng tiền tự động; thanh toán (tiền mặt: nhập tiền khách đưa và tính tiền thừa; chuyển khoản: hiện ảnh QR); xác nhận tạo đơn; xem/in hóa đơn; giữ đơn mở cho bàn đang phục vụ.
- **`/staff/customers`:** danh sách khách, tìm theo tên và số điện thoại, xem thông tin và lịch sử mua, cập nhật thông tin (◐ `EditCustomer`).
- **`/staff/availability` (◐ `ToggleProduct`):** bật/tắt nhanh trạng thái "Tạm hết" của món.
- **`/staff/reservations` (P1):** xem và xác nhận đặt bàn.
- **`/staff/profile`:** thông tin cá nhân, chức vụ, ca và lịch làm việc, đổi mật khẩu, đăng xuất.

### 7.3. Khu vực Admin (`_AdminLayout`)

Mục tiêu: chủ quán quản lý toàn bộ kinh doanh, nhân sự, sản phẩm, kho và doanh thu. Phong cách dashboard cao cấp: sidebar thu gọn được, breadcrumb, thẻ thống kê, biểu đồ, bảng dữ liệu (tìm kiếm, lọc, phân trang ở server), modal thêm/sửa/xác nhận xóa, toast thông báo.

- **`/admin/dashboard`:** thẻ tổng doanh thu, tổng đơn, tổng khách hàng, tổng sản phẩm, đơn đang xử lý, cảnh báo nguyên liệu sắp hết; biểu đồ doanh thu theo ngày/tuần/tháng; biểu đồ sản phẩm bán chạy; biểu đồ đơn theo trạng thái; danh sách đơn gần đây; bộ lọc thời gian (hôm nay, 7 ngày, 30 ngày, tùy chọn). **Doanh thu chỉ tính đơn "Hoàn thành" và "Đã thanh toán".**
- **`/admin/products`:** danh sách (ảnh, tên, danh mục, giá, trạng thái), tìm kiếm, lọc theo danh mục và trạng thái, phân trang. Form thêm/sửa gồm: tên, mô tả, upload ảnh (xem trước, kiểm tra định dạng và dung lượng), danh mục, giá cơ bản, size và giá theo size, topping áp dụng, tùy chọn nóng/lạnh/đường/đá, món nổi bật, trạng thái kinh doanh, tab **Công thức** nguyên liệu. Ẩn/hiện món; xóa mềm có xác nhận (món đã có đơn chỉ được ẩn).
- **`/admin/categories`:** CRUD danh mục, ảnh hoặc biểu tượng, thứ tự hiển thị, trạng thái hiển thị. Không xóa danh mục còn sản phẩm (yêu cầu chuyển sản phẩm hoặc ẩn).
- **`/admin/toppings`:** CRUD topping (tên, giá, trạng thái).
- **`/admin/orders`:** đơn toàn hệ thống (mã đơn, khách hàng, nhân viên phụ trách, chi tiết món, tổng tiền, phương thức thanh toán, trạng thái, thời gian đặt); tìm kiếm, lọc (trạng thái, ngày, thanh toán, loại đơn, nhân viên), phân trang; xem chi tiết kèm lịch sử trạng thái; cập nhật trạng thái; hủy đơn và hoàn tiền theo Mục 6.4; in hóa đơn; xuất Excel.
- **`/admin/employees`:** danh sách nhân viên; thêm (tạo tài khoản kèm mật khẩu tạm, bắt đổi mật khẩu ở lần đăng nhập đầu), chỉnh sửa, khóa/mở khóa, đặt lại mật khẩu; chức vụ, số điện thoại, email, trạng thái làm việc; gán quyền ◐ bằng checkbox; lịch ca theo tuần **(P1)** và lịch sử ca/đối soát tiền **(P1)**.
- **`/admin/customers`:** danh sách khách (thông tin cá nhân, số điện thoại, email, tổng số đơn, tổng chi tiêu tính trên đơn hoàn thành, điểm tích lũy **(P1)**), lịch sử mua hàng, tìm kiếm, lọc, sắp xếp, khóa tài khoản.
- **`/admin/tables`:** CRUD bàn (mã bàn, khu vực, sức chứa, trạng thái); chỉnh sơ đồ bàn (kéo-thả hoặc lưới); theo dõi tình trạng sử dụng theo thời gian thực; quản lý đặt trước **(P1)**.
- **`/admin/promotions`:** tạo/sửa chương trình (tên, mã, loại phần trăm hoặc số tiền, giá trị, mức giảm tối đa, đơn tối thiểu, thời gian bắt đầu và kết thúc, giới hạn tổng lượt và lượt mỗi người, trạng thái); xem số lượt đã dùng; tự đánh dấu "Hết hạn".
- **`/admin/inventory`:** danh sách nguyên liệu (mã, tên, đơn vị tính, số lượng tồn, ngưỡng cảnh báo, badge cảnh báo); nhập kho; xuất kho và điều chỉnh (bắt buộc lý do); lịch sử nhập xuất (lọc theo nguyên liệu, loại, ngày); cảnh báo sắp hết; xem và sửa công thức theo món.
- **`/admin/reports`:** doanh thu theo ngày, tháng, năm; số lượng đơn; sản phẩm bán chạy và bán chậm; hiệu suất nhân viên (số đơn, doanh thu theo từng Staff); thống kê khách hàng (mới, quay lại, chi tiêu cao nhất); báo cáo nhập xuất kho; bộ lọc thời gian; **xuất Excel (P0)** và **PDF (P1)**. Mọi số liệu truy vấn từ database.
- **`/admin/settings`:** các tab: Thông tin quán; Logo và hình ảnh (logo, banner, gallery); Màu thương hiệu (áp dụng qua CSS variables); Liên hệ và mạng xã hội; Giờ hoạt động; Phương thức thanh toán (bật/tắt, tải ảnh QR, thông tin chuyển khoản); Giao hàng **(P1)**; Tích điểm **(P1)**; Vai trò và quyền (gán permission cho Staff); Cấu hình hệ thống (định dạng, thời gian giữ bàn đặt trước).
- **`/admin/audit-logs` (P1):** nhật ký hoạt động, lọc theo người dùng, hành động, khoảng ngày.

---

## 8. QUY TRÌNH NGHIỆP VỤ CHÍNH

1. **Khách đặt hàng:** Xem thực đơn → chọn món → tùy chỉnh (size, nóng/lạnh, đường, đá, topping) → thêm giỏ → nhập mã giảm giá → đăng nhập → xác nhận đơn → chọn tiền mặt hoặc chuyển khoản QR → theo dõi đơn → nhận món.
2. **Nhân viên xử lý đơn:** Nhận thông báo đơn mới → tiếp nhận → xác nhận → pha chế (tự trừ kho) → cập nhật "Sẵn sàng nhận" → giao cho khách → xác nhận thu tiền → Hoàn thành.
3. **Bán hàng tại quầy (POS):** Chọn bàn hoặc mang đi → chọn món và tùy chỉnh → áp dụng khuyến mãi (nếu có quyền) → tính tiền → xác nhận thanh toán → đơn vào luồng pha chế → hoàn tất. Đơn tại bàn được giữ mở, thêm món và thanh toán khi khách rời bàn.
4. **Quản trị viên:** Đăng nhập Admin → Dashboard → quản lý sản phẩm, danh mục, topping, nhân viên, khách hàng, đơn hàng, bàn, kho, khuyến mãi → theo dõi doanh thu → xuất báo cáo.
5. **Đặt bàn (P1):** Khách gửi yêu cầu → Staff/Admin xác nhận hoặc từ chối → bàn hiển thị "Đã đặt trước" khi gần giờ → khách đến, Staff tạo đơn tại bàn.
6. **Ca làm (P1):** Mở ca (tiền đầu ca) → bán hàng và xử lý đơn → đóng ca (tiền thực đếm, chênh lệch) → báo cáo ca cho Admin.

---

## 9. HỆ THỐNG THIẾT KẾ (DESIGN SYSTEM)

Bộ nhận diện đồng nhất nhưng phân biệt rõ từng vai trò. Toàn bộ màu, cỡ chữ, khoảng cách, bo góc, đổ bóng khai báo bằng CSS variables trong `wwwroot/css/tokens.css`; màu thương hiệu lấy từ Mục 1 và có thể đổi trong Cài đặt.

**Customer:** hiện đại, thân thiện, giàu hình ảnh; tập trung vào sản phẩm và trải nghiệm mua hàng; ảnh đồ uống chất lượng cao; bố cục thoáng, dễ tìm và dễ đặt món bằng một tay trên điện thoại.

**Staff:** tối giản, trực quan; nút thao tác lớn (≥ 48 px); ưu tiên tốc độ xử lý đơn; ít trang trí; trạng thái bằng màu kèm biểu tượng và nhãn chữ (không chỉ dựa vào màu).

**Admin:** dashboard chuyên nghiệp, sidebar điều hướng thu gọn được, thẻ thống kê, biểu đồ, bảng dữ liệu có tìm kiếm/lọc/phân trang, modal thêm-sửa-xác nhận xóa, bộ lọc thời gian và trạng thái.

**Màu trạng thái đơn hàng (dùng thống nhất ở cả 3 khu vực):**

| Trạng thái | Màu gợi ý |
| --- | --- |
| Chờ xác nhận | Hổ phách `#F59E0B` |
| Đã xác nhận | Xanh dương `#3B82F6` |
| Đang pha chế | Tím `#8B5CF6` |
| Sẵn sàng nhận | Xanh lá `#10B981` |
| Hoàn thành | Xám đá `#64748B` |
| Đã hủy | Đỏ `#EF4444` |

Màu trạng thái bàn: Trống (xanh lá), Đang sử dụng (cam), Đã đặt trước (xanh dương).

**Quy chuẩn chung**

- Font hiện đại, dễ đọc (Be Vietnam Pro); bảng màu nhất quán; khoảng cách theo thang 4/8 px; border radius và shadow tinh tế; bộ icon đồng bộ (Bootstrap Icons).
- Không lạm dụng gradient và animation; tôn trọng `prefers-reduced-motion`.
- Đủ trạng thái hover, focus (hiển thị rõ), active, disabled; tương phản chữ đạt WCAG AA; thẻ `alt` cho ảnh; điều hướng được bằng bàn phím.
- Component dùng chung (partial hoặc ViewComponent): ProductCard, StatusBadge, Pagination, ConfirmModal, Toast, EmptyState, LoadingSkeleton, StatCard, DataTable, FormField, ImageUploader.

---

## 10. BẢO MẬT VÀ CHẤT LƯỢNG

- Anti-forgery token cho mọi form và request AJAX thay đổi dữ liệu; HTTPS, cookie `HttpOnly` và `SameSite`.
- Chính sách mật khẩu và khóa tài khoản theo Mục 4.2 và 5.1; giới hạn tần suất đăng nhập (rate limiting).
- Validate ở cả client và server; mã hóa đầu ra (Razor mặc định), làm sạch ghi chú tự do; truy vấn tham số hóa qua EF Core.
- Upload ảnh: kiểm tra phần mở rộng, content-type và magic bytes; tối đa 2 MB; chỉ jpg/png/webp (không nhận SVG); đổi tên bằng GUID; xóa ảnh cũ khi thay.
- Thêm header bảo mật cơ bản (`X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`, CSP phù hợp).
- Không lộ stack trace, connection string hay thông tin nhạy cảm; thông báo lỗi thân thiện.
- Không tin dữ liệu từ client: giá, tổng tiền, quyền, trạng thái đều kiểm tra lại ở server.

---

## 11. DỮ LIỆU KHỞI TẠO (SEED) VÀ HÌNH ẢNH

"Không dùng dữ liệu giả" nghĩa là giao diện luôn đọc từ database và mọi nút chạy thật. Dữ liệu khởi tạo là dữ liệu ban đầu được nạp vào database bằng seeder để hệ thống chạy lên là dùng được.

**Seeder** chạy khi database trống, tách thành lớp riêng trong `Data/Seed`:

- 3 role: Admin, Staff, Customer.
- Tài khoản test (mật khẩu đọc từ `appsettings.Development.json`, chỉ seed ở môi trường Development, ở Production bắt buộc đổi):
  - Admin: `admin@coffee.local` / `Admin@123`
  - Staff (có đủ quyền ◐ để test): `staff@coffee.local` / `Staff@123`
  - Staff 2 (không có quyền ◐): `staff2@coffee.local` / `Staff@123`
  - Customer: `customer@coffee.local` / `Customer@123`
- 5 danh mục (Cà phê, Trà, Trà sữa, Nước ép và sinh tố, Bánh ngọt); ít nhất 20 sản phẩm có ảnh, mô tả, size và giá theo size; 6 đến 8 topping gắn đúng món.
- 12 đến 16 bàn chia 2 đến 3 khu vực (ví dụ Tầng 1, Tầng 2, Sân vườn).
- 3 khuyến mãi: 1 phần trăm đang chạy, 1 giảm số tiền có đơn tối thiểu, 1 đã hết hạn.
- 15 đến 20 nguyên liệu có tồn kho (ít nhất 2 nguyên liệu dưới ngưỡng để thấy cảnh báo) và công thức cho mọi món.
- Phương thức thanh toán và `SystemSetting` mặc định theo Mục 1.
- **Đơn hàng demo (tùy chọn):** cờ `Seed:DemoOrders = true`, chỉ chạy ở Development, tạo khoảng 60 đơn rải trong 30 ngày qua **bằng chính `OrderService`** (đi qua đúng quy trình, trạng thái đa dạng) để dashboard và báo cáo có dữ liệu. Có thể tắt.

**Nguồn ảnh (mặc định):** dùng ảnh miễn phí bản quyền (Unsplash hoặc Pexels), tải về lưu tại `wwwroot/images/seed/`, không hotlink khi chạy, ghi nguồn tác giả vào `docs/IMAGE_CREDITS.md`. Nếu môi trường không có internet, tạo ảnh placeholder SVG cục bộ theo từng danh mục và ghi chú để Admin thay bằng ảnh thật qua chức năng upload. Nếu bạn tự cung cấp ảnh: **\[ĐƯỜNG DẪN THƯ MỤC ẢNH\]**.

---

## 12. YÊU CẦU RESPONSIVE

Hoạt động tốt trên desktop, laptop, tablet và smartphone.

- **Customer:** đặt món dễ dàng trên điện thoại; thanh giỏ hàng cố định phía dưới trên mobile; nút đủ lớn cho cảm ứng.
- **Staff:** thao tác nhanh trên máy tính bảng; POS và sơ đồ bàn dùng được ở cả chiều ngang và chiều dọc.
- **Admin:** theo dõi dữ liệu tốt nhất trên desktop, vẫn dùng được trên tablet và mobile.
- Sidebar thu gọn thành menu trượt trên màn hình nhỏ.
- Bảng dữ liệu cuộn ngang trong khung riêng hoặc chuyển thành dạng thẻ trên mobile.
- Không xuất hiện lỗi tràn ngang; vùng chạm tối thiểu 44 px; ảnh co giãn đúng tỉ lệ.
- Kiểm tra tối thiểu ở các độ rộng 360, 768, 1024, 1440 px.

---

## 13. KẾ HOẠCH TRIỂN KHAI THEO GIAI ĐOẠN

Làm lần lượt, **dừng báo cáo sau mỗi giai đoạn** theo quy tắc Mục 0.

| Giai đoạn | Nội dung | Tiêu chí hoàn thành |
| --- | --- | --- |
| 0. Phân tích | Kiểm tra `dotnet --version`, cấu trúc dự án, xác định có sẵn hay mới; viết `docs/ERD.md`, `docs/PERMISSIONS.md`, `docs/ASSUMPTIONS.md`; liệt kê module và luồng nghiệp vụ | 3 file docs hoàn tất; báo cáo các quyết định; chưa cần code |
| 1. Nền tảng | Project, DbContext, entity, migration, Identity + 3 role + policy, 3 layout, design tokens, component dùng chung, seed, trang lỗi 403/404/500, SettingService, đăng nhập/đăng ký/đổi mật khẩu/đăng xuất, chuyển hướng theo role | `dotnet build` không lỗi; `database update` chạy được; 4 tài khoản test vào đúng khu vực; Customer truy cập `/admin` bị 403 |
| 2. Admin lõi | Danh mục, sản phẩm (size, upload ảnh), topping, bàn, nhân viên, cài đặt cơ bản | CRUD đầy đủ, validate đúng, tìm kiếm/lọc/phân trang chạy |
| 3. Customer | Trang chủ, menu, chi tiết, giỏ hàng, checkout (`PricingService`, `PromotionService`), đơn hàng, tài khoản, giới thiệu/liên hệ | Đặt hàng online thành công; giá server khớp; đơn lưu đúng trong database |
| 4. Staff | Dashboard, quản lý đơn + chuyển trạng thái + SignalR, bàn, POS, khách hàng, hồ sơ | Chạy trọn Quy trình 2 và 3; thông báo đơn mới real-time |
| 5. Admin vận hành | Đơn hàng, khách hàng, khuyến mãi, kho + công thức + trừ kho, dashboard, báo cáo + Excel, cài đặt đầy đủ, phân quyền ◐ | Trừ kho đúng; số liệu dashboard khớp database; file Excel mở được |
| 6. Hoàn thiện | Làm P1 theo thứ tự: ca làm, đặt bàn, giao hàng, tích điểm, quên mật khẩu, audit log, in hóa đơn, PDF, unit test; sau đó P2 nếu còn thời gian; tối ưu responsive và hiệu năng; kiểm thử toàn diện; viết tài liệu | Qua toàn bộ checklist Mục 14; README đầy đủ |

---

## 14. KIỂM TRA TRƯỚC KHI BÀN GIAO

1. `dotnet build` không lỗi; tạo và áp dụng migration thành công trên database trống.
2. Tất cả trang và đường dẫn hoạt động, không có link chết.
3. Đăng nhập, đăng xuất, đổi mật khẩu; phân quyền đúng theo `docs/PERMISSIONS.md` (thử cả 3 role và Staff có/không có quyền ◐; truy cập trực tiếp bằng URL vẫn bị chặn).
4. Thêm, sửa, xóa mềm, xem dữ liệu ở mọi module quản trị; ràng buộc validate hoạt động.
5. Quy trình đặt hàng online và xử lý đơn từ đầu đến cuối; hủy đơn theo đúng quy tắc 6.4.
6. Giỏ hàng và tổng tiền đúng với mọi tổ hợp size, topping, số lượng, mã giảm giá; sửa giá ở client không làm đổi giá server.
7. Trạng thái đơn đồng bộ giữa Customer, Staff, Admin; thông báo đơn mới đến Staff.
8. Kho: trừ đúng theo công thức, không âm, cảnh báo sắp hết đúng ngưỡng.
9. Dashboard và báo cáo khớp số liệu trong database (đối chiếu bằng truy vấn trực tiếp); xuất Excel đúng.
10. Tìm kiếm, lọc, phân trang hoạt động ở mọi danh sách.
11. Responsive ở các độ rộng ở Mục 12; không tràn ngang.
12. Không có lỗi trong Console trình duyệt và log ứng dụng khi chạy các luồng chính.
13. **(P1)** Unit test (xUnit) cho `PricingService`, `PromotionService`, chuyển trạng thái đơn, `InventoryService`; ghi kết quả vào `docs/TEST_REPORT.md`.

---

## 15. KẾT QUẢ BÀN GIAO

- Source code có cấu trúc rõ ràng, không dồn vào một file.
- Giao diện Customer, Staff, Admin; hệ thống đăng nhập và phân quyền; các chức năng theo từng vai trò.
- Database, migration, seeder; quy trình đặt hàng và xử lý đơn; dashboard và báo cáo.
- `README.md`: yêu cầu môi trường (SDK, SQL Server), cấu hình connection string, lệnh `dotnet ef database update`, cách chạy, danh sách tài khoản test, các cờ cấu hình (ví dụ `Seed:DemoOrders`).
- Thư mục `docs/`: ERD, ASSUMPTIONS, PERMISSIONS, TEST\_REPORT, IMAGE\_CREDITS.
- Báo cáo cuối: tính năng đã hoàn thành theo P0/P1/P2, phần chưa làm kèm lý do, lỗi đã biết.

---

## 16. NGUYÊN TẮC BẮT BUỘC VÀ LỆNH BẮT ĐẦU

- Không chỉ tạo giao diện tĩnh hoặc bản minh họa: mọi nút và chức năng phải hoạt động đúng mục đích.
- Không bỏ qua các nghiệp vụ quan trọng ở Mục 6; không bỏ qua phân quyền ở phía server.
- Không tự ý thay đổi framework hoặc cấu trúc dự án hiện có.
- Không viết toàn bộ hệ thống trong một file; không tạo giao diện sơ sài.
- Không hard-code mật khẩu, khóa bí mật, connection string.
- Không tự ý tích hợp dịch vụ thanh toán bên thứ ba.
- Hoàn thiện từng module và kiểm tra trước khi sang module tiếp theo; không báo hoàn thành khi chưa build và chạy thử.
- Khi chưa chắc, chọn phương án đơn giản nhất phù hợp MVP và ghi vào `docs/ASSUMPTIONS.md`; chỉ hỏi lại khi bị chặn thực sự.

**Hãy bắt đầu ngay bằng Giai đoạn 0:** phân tích cấu trúc dự án hiện tại, xác nhận phiên bản .NET, lập danh sách module và luồng nghiệp vụ, rồi báo cáo trước khi viết code ở Giai đoạn 1.
