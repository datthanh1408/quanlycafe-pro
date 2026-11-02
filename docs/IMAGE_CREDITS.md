# Nguồn Hình Ảnh & Tài Nguyên Đồ Họa (IMAGE CREDITS) – Đạt Mộc Coffee

Theo quy định tại **Mục 11 (Nguồn ảnh mặc định)** của đặc tả dự án:
- Hệ thống **không sử dụng hotlink internet** khi chạy thực tế để đảm bảo ứng dụng hoạt động ổn định 100% trong môi trường cục bộ / không có internet.
- Toàn bộ 22 sản phẩm trong 5 danh mục (`Cà phê`, `Trà trái cây`, `Trà sữa`, `Nước ép và sinh tố`, `Bánh ngọt`) được sinh bộ ảnh minh họa SVG cục bộ chất lượng cao tại `wwwroot/images/seed/*.svg` thông qua `Data/Seed/SeedArtworkGenerator.cs` (bản quyền nội bộ dự án – CC0 / First-party generated artwork).
- Quản trị viên (`Admin`) có thể thay thế bất kỳ hình ảnh sản phẩm, ảnh đại diện hoặc ảnh mã QR ngân hàng nào bằng tệp ảnh thật (`JPG`, `PNG`, `WEBP`, dung lượng tối đa `2 MB`) thông qua chức năng Upload tại `/admin/products` và `/admin/settings` (lưu vào `wwwroot/uploads/`).

## Danh sách tệp hình ảnh khởi tạo (`wwwroot/images/seed/`)

| Tệp ảnh | Sản phẩm / Mục đích | Bản quyền |
| --- | --- | --- |
| `ca-phe-den.svg` | Cà Phê Đen Đá Mộc | Nội bộ dự án (First-party SVG) |
| `ca-phe-sua.svg` | Cà Phê Sữa Đá Sài Gòn | Nội bộ dự án (First-party SVG) |
| `bac-xiu.svg` | Bạc Xỉu Cốt Dừa Đạt Mộc | Nội bộ dự án (First-party SVG) |
| `espresso.svg` | Espresso Nguyên Bản Cầu Đất | Nội bộ dự án (First-party SVG) |
| `latte.svg` | Caramel Latte Nghệ Thuật | Nội bộ dự án (First-party SVG) |
| `cold-brew.svg` | Cold Brew Cam Sả Thanh Mát | Nội bộ dự án (First-party SVG) |
| `tra-dao-cam-sa.svg` | Trà Đào Cam Sả Đạt Mộc | Nội bộ dự án (First-party SVG) |
| `tra-vai-hoa-hong.svg` | Trà Vải Hoa Hồng Đà Lạt | Nội bộ dự án (First-party SVG) |
| `tra-sen-vang.svg` | Trà Ô Long Sen Vàng Macchiato | Nội bộ dự án (First-party SVG) |
| `tra-xanh-nhai.svg` | Lục Trà Hoa Nhài Nguyên Lá | Nội bộ dự án (First-party SVG) |
| `tra-sua-tran-chau.svg` | Trà Sữa Trân Châu Đường Đen | Nội bộ dự án (First-party SVG) |
| `tra-sua-oolong.svg` | Trà Sữa Ô Long Rang Đậm Vị | Nội bộ dự án (First-party SVG) |
| `matcha-latte.svg` | Matcha Latte Uji Kem Mịn | Nội bộ dự án (First-party SVG) |
| `tra-sua-khoai-mon.svg` | Trà Sữa Khoai Môn Tươi Nghiền | Nội bộ dự án (First-party SVG) |
| `nuoc-ep-cam.svg` | Nước Ép Cam Vàng Nguyên Chất | Nội bộ dự án (First-party SVG) |
| `nuoc-ep-dua-hau.svg` | Nước Ép Dưa Hấu Bạc Hà | Nội bộ dự án (First-party SVG) |
| `sinh-to-bo.svg` | Sinh Tố Bơ Sáp Đắk Lắk | Nội bộ dự án (First-party SVG) |
| `sinh-to-xoai.svg` | Sinh Tố Xoài Cát Hòa Lộc | Nội bộ dự án (First-party SVG) |
| `croissant.svg` | Bánh Sừng Bò Bơ Pháp (Croissant) | Nội bộ dự án (First-party SVG) |
| `tiramisu.svg` | Tiramisu Cà Phê Ý Thủ Công | Nội bộ dự án (First-party SVG) |
| `cheesecake.svg` | Chanh Dây Phô Mai (Cheesecake) | Nội bộ dự án (First-party SVG) |
| `banh-mi-que.svg` | Bánh Mì Que Pate Cột Đèn | Nội bộ dự án (First-party SVG) |
| `default-drink.svg` | Ảnh mặc định dự phòng | Nội bộ dự án (First-party SVG) |

## Mô hình 3D WebGL (Three.js)

- Toàn bộ mô hình không gian quán cà phê 3D (quầy bar, máy pha espresso, đèn thả trần, cây xanh sân vườn, bàn ghế 3 khu vực, tách cà phê bốc khói) và mô hình ly đồ uống 3D tương tác (size S/M/L, cốc sứ nóng, ly lạnh đá viên 0–100%, trân châu, lớp foam macchiato) được dựng trực tiếp bằng hình học thủ tục (Procedural PBR Geometry & Materials) trên **Three.js v0.170.0** (MIT License, lưu cục bộ tại `wwwroot/lib/three/`).

