using System.Text;

namespace CoffeeShop.Data.Seed;

/// <summary>
/// Tạo bộ hình ảnh SVG minh họa nghệ thuật cục bộ cho từng sản phẩm và không gian quán tại wwwroot/images/seed/
/// giúp hệ thống hiển thị sắc nét 100% khi không có internet (Mục 11). Admin có thể thay thế bằng ảnh thật qua Upload.
/// </summary>
public static class SeedArtworkGenerator
{
    public static void EnsureSeedImages(string webRootPath)
    {
        var dir = Path.Combine(webRootPath, "images", "seed");
        Directory.CreateDirectory(dir);

        var items = new (string FileName, string Title, string Subtitle, string Bg1, string Bg2, string Liquid, string Kind)[]
        {
            ("default-drink.svg", "Đạt Mộc Coffee", "Artisan Beverage", "#3E2723", "#6F4E37", "#4A2C18", "cup"),
            ("ca-phe-den.svg", "Cà Phê Đen Đá", "100% Robusta & Arabica", "#2C1810", "#5C3A21", "#1F110A", "iced"),
            ("ca-phe-sua.svg", "Cà Phê Sữa Đá", "Đậm vị truyền thống", "#3D2314", "#8B5A2B", "#6F4E37", "iced-milk"),
            ("bac-xiu.svg", "Bạc Xỉu Cốt Dừa", "Béo thơm dịu ngọt", "#5C4033", "#C89D7C", "#C69C72", "iced-milk"),
            ("espresso.svg", "Espresso Nguyên Bản", "Single / Double Shot", "#1E120D", "#4A2E1B", "#2B170E", "hot"),
            ("latte.svg", "Caramel Latte Nghệ Thuật", "Arabica Cầu Đất", "#4E342E", "#A1887F", "#9C7355", "hot"),
            ("cold-brew.svg", "Cold Brew Cam Sả", "Ủ lạnh 18 giờ", "#3E2723", "#C8782A", "#5A2D0C", "iced"),
            ("tra-dao-cam-sa.svg", "Trà Đào Cam Sả", "Thanh mát sảng khoái", "#7C2D12", "#EA580C", "#D97706", "tea"),
            ("tra-vai-hoa-hong.svg", "Trà Vải Hoa Hồng", "Hương hoa dịu nhẹ", "#831843", "#DB2777", "#E11D48", "tea"),
            ("tra-sen-vang.svg", "Trà Ô Long Sen Vàng", "Kem Macchiato mịn", "#713F12", "#CA8A04", "#B45309", "tea"),
            ("tra-xanh-nhai.svg", "Lục Trà Hoa Nhài", "Đồi chè Bảo Lộc", "#14532D", "#16A34A", "#65A30D", "tea"),
            ("tra-sua-tran-chau.svg", "Trà Sữa Đường Đen", "Trân châu nấu ấm", "#3B2314", "#92400E", "#B08968", "milktea"),
            ("tra-sua-oolong.svg", "Trà Sữa Ô Long Rang", "Đậm vị trà nướng", "#422006", "#854D0E", "#A47551", "milktea"),
            ("matcha-latte.svg", "Matcha Latte Uji", "Bột trà xanh Nhật Bản", "#14532D", "#4ADE80", "#4D7C0F", "milktea"),
            ("tra-sua-khoai-mon.svg", "Trà Sữa Khoai Môn", "Khoai môn nghiền tươi", "#3B0764", "#9333EA", "#9333EA", "milktea"),
            ("nuoc-ep-cam.svg", "Nước Ép Cam Tươi", "100% Cam nguyên chất", "#9A3412", "#F97316", "#FB923C", "juice"),
            ("nuoc-ep-dua-hau.svg", "Nước Ép Dưa Hấu", "Ngọt thanh tự nhiên", "#7F1D1D", "#EF4444", "#DC2626", "juice"),
            ("sinh-to-bo.svg", "Sinh Tố Bơ Sáp Đắk Lắk", "Sánh mịn béo ngậy", "#14532D", "#65A30D", "#84CC16", "juice"),
            ("sinh-to-xoai.svg", "Sinh Tố Xoài Cát", "Xoài chín cây tươi", "#854D0E", "#EAB308", "#FACC15", "juice"),
            ("croissant.svg", "Bánh Sừng Bò Bơ Pháp", "Nướng mới mỗi sáng", "#78350F", "#D97706", "#F59E0B", "bakery"),
            ("tiramisu.svg", "Tiramisu Cà Phê Ý", "Mascarpone & Espresso", "#3E2723", "#795548", "#5D4037", "bakery"),
            ("cheesecake.svg", "Chanh Dây Cheesecake", "Chua dịu phô mai", "#713F12", "#FBBF24", "#FDE047", "bakery"),
            ("banh-mi-que.svg", "Bánh Mì Que Pate Cột Đèn", "Giòn nóng thơm lừng", "#7C2D12", "#C2410C", "#EA580C", "bakery")
        };

        foreach (var item in items)
        {
            var path = Path.Combine(dir, item.FileName);
            if (!File.Exists(path))
            {
                File.WriteAllText(path, BuildProductSvg(item.Title, item.Subtitle, item.Bg1, item.Bg2, item.Liquid, item.Kind), Encoding.UTF8);
            }
        }
    }

    private static string BuildProductSvg(string title, string subtitle, string bg1, string bg2, string liquid, string kind)
    {
        string illustration = kind switch
        {
            "hot" => $@"
                <ellipse cx='200' cy='255' rx='85' ry='14' fill='rgba(0,0,0,0.28)'/>
                <path d='M125 155 Q125 245 200 245 Q275 245 275 155 Z' fill='#F6EFE7' stroke='#E2D5C5' stroke-width='3'/>
                <path d='M272 170 Q305 170 305 198 Q305 224 265 224' fill='none' stroke='#F6EFE7' stroke-width='12' stroke-linecap='round'/>
                <ellipse cx='200' cy='155' rx='75' ry='20' fill='#FFFDF9'/>
                <ellipse cx='200' cy='155' rx='66' ry='16' fill='{liquid}'/>
                <path d='M180 155 Q200 143 220 155 Q200 167 180 155' fill='#F6EFE7' opacity='0.65'/>
                <path d='M180 120 Q170 98 185 80' fill='none' stroke='rgba(255,255,255,0.5)' stroke-width='4' stroke-linecap='round'/>
                <path d='M205 115 Q195 90 212 70' fill='none' stroke='rgba(255,255,255,0.6)' stroke-width='4' stroke-linecap='round'/>
                <path d='M228 122 Q218 100 232 82' fill='none' stroke='rgba(255,255,255,0.4)' stroke-width='4' stroke-linecap='round'/>",

            "bakery" => $@"
                <ellipse cx='200' cy='248' rx='105' ry='18' fill='rgba(0,0,0,0.28)'/>
                <ellipse cx='200' cy='235' rx='98' ry='22' fill='#F6EFE7' stroke='#D7C4B0' stroke-width='3'/>
                <path d='M130 220 Q145 140 200 135 Q255 140 270 220 Q200 238 130 220 Z' fill='{liquid}' stroke='#FFF3E0' stroke-width='2'/>
                <path d='M152 185 Q200 162 248 185' fill='none' stroke='#FFF8E7' stroke-width='5' stroke-linecap='round' opacity='0.7'/>
                <circle cx='200' cy='135' r='10' fill='#DC2626'/>",

            "milktea" => $@"
                <ellipse cx='200' cy='265' rx='68' ry='12' fill='rgba(0,0,0,0.3)'/>
                <rect x='194' y='62' width='12' height='185' rx='5' fill='#2C1810'/>
                <polygon points='145,108 255,108 240,258 160,258' fill='rgba(255,255,255,0.25)' stroke='rgba(255,255,255,0.7)' stroke-width='3'/>
                <polygon points='149,130 251,130 238,254 162,254' fill='{liquid}' opacity='0.92'/>
                <path d='M147 110 Q200 120 253 110 L251 134 Q200 142 149 134 Z' fill='#FFFBEB'/>
                <circle cx='175' cy='242' r='6' fill='#1C110A'/><circle cx='192' cy='245' r='6' fill='#1C110A'/>
                <circle cx='210' cy='243' r='6' fill='#1C110A'/><circle cx='226' cy='240' r='6' fill='#1C110A'/>
                <circle cx='184' cy='230' r='6' fill='#1C110A'/><circle cx='203' cy='231' r='6' fill='#1C110A'/>
                <circle cx='219' cy='228' r='6' fill='#1C110A'/>",

            _ => $@"
                <ellipse cx='200' cy='265' rx='68' ry='12' fill='rgba(0,0,0,0.3)'/>
                <path d='M210 66 L228 60 L202 240' fill='none' stroke='#F6EFE7' stroke-width='8' stroke-linecap='round'/>
                <polygon points='146,105 254,105 238,258 162,258' fill='rgba(255,255,255,0.2)' stroke='rgba(255,255,255,0.75)' stroke-width='3'/>
                <polygon points='150,124 250,124 236,254 164,254' fill='{liquid}' opacity='0.9'/>
                <rect x='166' y='136' width='22' height='20' rx='4' fill='rgba(255,255,255,0.55)' transform='rotate(-12 177 146)'/>
                <rect x='204' y='142' width='22' height='20' rx='4' fill='rgba(255,255,255,0.5)' transform='rotate(14 215 152)'/>
                <rect x='184' y='165' width='22' height='20' rx='4' fill='rgba(255,255,255,0.42)' transform='rotate(6 195 175)'/>"
        };

        return $@"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 400 340' width='400' height='340'>
  <defs>
    <linearGradient id='bg' x1='0%' y1='0%' x2='100%' y2='100%'>
      <stop offset='0%' stop-color='{bg1}'/>
      <stop offset='100%' stop-color='{bg2}'/>
    </linearGradient>
    <radialGradient id='glow' cx='50%' cy='45%' r='50%'>
      <stop offset='0%' stop-color='rgba(255,245,230,0.28)'/>
      <stop offset='100%' stop-color='rgba(255,245,230,0)'/>
    </radialGradient>
  </defs>
  <rect width='400' height='340' fill='url(#bg)' rx='16'/>
  <circle cx='200' cy='165' r='145' fill='url(#glow)'/>
  {illustration}
  <text x='200' y='300' text-anchor='middle' fill='#FFFDF9' font-family='Segoe UI, sans-serif' font-size='17' font-weight='700'>{System.Net.WebUtility.HtmlEncode(title)}</text>
  <text x='200' y='321' text-anchor='middle' fill='rgba(255,246,235,0.8)' font-family='Segoe UI, sans-serif' font-size='12'>{System.Net.WebUtility.HtmlEncode(subtitle)}</text>
</svg>";
    }
}

