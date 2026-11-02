using System.Security.Claims;
using CoffeeShop.Authorization;
using CoffeeShop.Models.Entities;
using CoffeeShop.Models.Enums;
using CoffeeShop.Models.ViewModels;
using CoffeeShop.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShop.Data.Seed;

public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, IWebHostEnvironment env, IConfiguration config)
    {
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("DbInitializer");
        var db = services.GetRequiredService<AppDbContext>();

        if (db.Database.IsSqlite())
        {
            await db.Database.EnsureCreatedAsync();
        }
        else
        {
            await db.Database.MigrateAsync();
        }

        // Đảm bảo bộ ảnh SVG cục bộ cho toàn bộ món seed luôn sẵn sàng
        SeedArtworkGenerator.EnsureSeedImages(env.WebRootPath);

        // 1. Seed Roles
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { AppRoles.Admin, AppRoles.Staff, AppRoles.Customer })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        // 2. Seed SystemSettings & PaymentMethods nếu trống
        if (!await db.SystemSettings.AnyAsync())
        {
            var defaultSettings = new Dictionary<string, string>
            {
                ["ShopName"] = "Đạt Mộc Coffee",
                ["Slogan"] = "Hương mộc nguyên bản – Trọn vị từng khoảnh khắc",
                ["BusinessType"] = "Cafe & Bakery",
                ["Address"] = "128 Nguyễn Huệ, Phường Bến Nghé, Quận 1, TP. Hồ Chí Minh (Dữ liệu mẫu)",
                ["Phone"] = "0900 000 000",
                ["Email"] = "contact@example.com",
                ["OpeningHours"] = "07:00 – 22:00 hằng ngày",
                ["FacebookUrl"] = "",
                ["ZaloUrl"] = "",
                ["GoogleMapsEmbedUrl"] = "",
                ["PrimaryColor"] = "#6F4E37",
                ["SecondaryColor"] = "#F6EFE7",
                ["AccentColor"] = "#C8782A",
                ["BankName"] = "",
                ["BankAccountNumber"] = "",
                ["BankAccountHolder"] = "",
                ["QrImageUrl"] = "",
                ["ShippingFee"] = "15000",
                ["FreeShippingThreshold"] = "150000",
                ["LoyaltyEarnPerVnd"] = "10000",
                ["LoyaltyRedeemValueVnd"] = "100",
                ["ReservationHoldMinutes"] = "30"
            };

            foreach (var kv in defaultSettings)
            {
                db.SystemSettings.Add(new SystemSetting { Key = kv.Key, Value = kv.Value, UpdatedAt = DateTime.UtcNow });
            }
            await db.SaveChangesAsync();
        }

        if (!await db.PaymentMethods.AnyAsync())
        {
            db.PaymentMethods.AddRange(
                new PaymentMethodConfig { Code = "CASH", Name = "Tiền mặt khi nhận món / tại quầy", IsActive = true, DisplayOrder = 1 },
                new PaymentMethodConfig { Code = "QR", Name = "Chuyển khoản QR Ngân hàng", IsActive = true, DisplayOrder = 2 },
                new PaymentMethodConfig { Code = "CARD", Name = "Thẻ qua máy POS tại quầy", IsActive = false, DisplayOrder = 3 }
            );
            await db.SaveChangesAsync();
        }

        // 3. Seed Tài khoản test (chỉ chạy ở môi trường Development theo Mục 11, đọc mật khẩu từ appsettings.Development.json)
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        ApplicationUser? staff1User = null;
        ApplicationUser? staff2User = null;
        ApplicationUser? customer1User = null;

        if (env.IsDevelopment())
        {
            var accSection = config.GetSection("Seed:TestAccounts");
            var adminEmail = accSection["AdminEmail"] ?? "admin@coffee.local";
            var adminPwd = accSection["AdminPassword"];
            var staffEmail = accSection["StaffEmail"] ?? "staff@coffee.local";
            var staffPwd = accSection["StaffPassword"];
            var staff2Email = accSection["Staff2Email"] ?? "staff2@coffee.local";
            var staff2Pwd = accSection["Staff2Password"];
            var custEmail = accSection["CustomerEmail"] ?? "customer@coffee.local";
            var custPwd = accSection["CustomerPassword"];

            if (!string.IsNullOrWhiteSpace(adminPwd))
            {
                await EnsureUserAsync(userManager, db, adminEmail, adminPwd, "Quản Trị Viên Đạt Mộc", "0901234567", AppRoles.Admin,
                    employeePos: "Quản lý cửa hàng", permissions: null);
            }

            if (!string.IsNullOrWhiteSpace(staffPwd))
            {
                staff1User = await EnsureUserAsync(userManager, db, staffEmail, staffPwd, "Nguyễn Minh Khôi (Ca trưởng)", "0912345678", AppRoles.Staff,
                    employeePos: "Ca trưởng Barista", permissions: StaffPermissions.All.Keys.ToList());
            }

            if (!string.IsNullOrWhiteSpace(staff2Pwd))
            {
                staff2User = await EnsureUserAsync(userManager, db, staff2Email, staff2Pwd, "Trần Thảo Vy (Phục vụ)", "0987654321", AppRoles.Staff,
                    employeePos: "Nhân viên phục vụ", permissions: null);
            }

            if (!string.IsNullOrWhiteSpace(custPwd))
            {
                customer1User = await EnsureUserAsync(userManager, db, custEmail, custPwd, "Lê Hoàng Nam", "0938112233", AppRoles.Customer,
                    employeePos: null, permissions: null, address: "45 Lê Lợi, Quận 1, TP. Hồ Chí Minh");
            }
        }

        // 4. Nếu đã có danh mục & sản phẩm thì dừng để không ghi đè dữ liệu thật
        if (await db.Categories.AnyAsync())
        {
            return;
        }

        logger.LogInformation("Đang khởi tạo dữ liệu mẫu ban đầu cho Đạt Mộc Coffee...");

        // 5 Danh mục
        var catCoffee = new Category { Name = "Cà phê", Slug = "ca-phe", IconClass = "bi-cup-hot-fill", DisplayOrder = 1, IsActive = true };
        var catTea = new Category { Name = "Trà trái cây", Slug = "tra-trai-cay", IconClass = "bi-flower1", DisplayOrder = 2, IsActive = true };
        var catMilkTea = new Category { Name = "Trà sữa", Slug = "tra-sua", IconClass = "bi-cup-straw", DisplayOrder = 3, IsActive = true };
        var catJuice = new Category { Name = "Nước ép và sinh tố", Slug = "nuoc-ep-sinh-to", IconClass = "bi-droplet-half", DisplayOrder = 4, IsActive = true };
        var catBakery = new Category { Name = "Bánh ngọt", Slug = "banh-ngot", IconClass = "bi-cake2-fill", DisplayOrder = 5, IsActive = true };

        db.Categories.AddRange(catCoffee, catTea, catMilkTea, catJuice, catBakery);
        await db.SaveChangesAsync();

        // 7 Topping
        var topBlackBoba = new Topping { Name = "Trân châu đen đường đen", Price = 8000, IsActive = true };
        var topWhiteBoba = new Topping { Name = "Trân châu trắng giòn", Price = 8000, IsActive = true };
        var topMacchiato = new Topping { Name = "Kem phô mai Macchiato", Price = 12000, IsActive = true };
        var topCoffeeJelly = new Topping { Name = "Thạch cà phê nguyên chất", Price = 8000, IsActive = true };
        var topPeach = new Topping { Name = "Đào miếng ngâm giòn", Price = 10000, IsActive = true };
        var topEspresso = new Topping { Name = "Thêm 1 Shot Espresso", Price = 12000, IsActive = true };
        var topOatmilk = new Topping { Name = "Đổi sữa yến mạch Oatmilk", Price = 15000, IsActive = true };

        db.Toppings.AddRange(topBlackBoba, topWhiteBoba, topMacchiato, topCoffeeJelly, topPeach, topEspresso, topOatmilk);
        await db.SaveChangesAsync();

        // 18 Nguyên liệu (có 3 nguyên liệu tồn kho dưới ngưỡng cảnh báo theo Mục 11!)
        var ingRobusta = new Ingredient { Code = "NL-001", Name = "Hạt cà phê Robusta Đắk Lắk", Unit = "g", StockQty = 18000, MinThreshold = 3000 };
        var ingArabica = new Ingredient { Code = "NL-002", Name = "Hạt cà phê Arabica Cầu Đất", Unit = "g", StockQty = 850, MinThreshold = 1500 }; // Cảnh báo sắp hết!
        var ingCondensedMilk = new Ingredient { Code = "NL-003", Name = "Sữa đặc có đường", Unit = "ml", StockQty = 25000, MinThreshold = 4000 };
        var ingFreshMilk = new Ingredient { Code = "NL-004", Name = "Sữa tươi thanh trùng", Unit = "ml", StockQty = 35000, MinThreshold = 5000 };
        var ingCoconutCream = new Ingredient { Code = "NL-005", Name = "Cốt dừa béo nguyên chất", Unit = "ml", StockQty = 8000, MinThreshold = 1500 };
        var ingBlackTea = new Ingredient { Code = "NL-006", Name = "Cốt hồng trà Thượng Hạng", Unit = "ml", StockQty = 30000, MinThreshold = 5000 };
        var ingOolongTea = new Ingredient { Code = "NL-007", Name = "Cốt trà Ô Long Bảo Lộc", Unit = "ml", StockQty = 28000, MinThreshold = 5000 };
        var ingJasmineTea = new Ingredient { Code = "NL-008", Name = "Lục trà lài ủ lạnh", Unit = "ml", StockQty = 22000, MinThreshold = 4000 };
        var ingMatcha = new Ingredient { Code = "NL-009", Name = "Bột Matcha Uji Nhật Bản", Unit = "g", StockQty = 2500, MinThreshold = 500 };
        var ingPeachSlice = new Ingredient { Code = "NL-010", Name = "Đào ngâm miếng đóng hộp", Unit = "g", StockQty = 650, MinThreshold = 1200 }; // Cảnh báo sắp hết!
        var ingLychee = new Ingredient { Code = "NL-011", Name = "Trái vải ngâm đường phèn", Unit = "g", StockQty = 5000, MinThreshold = 1000 };
        var ingSugarSyrup = new Ingredient { Code = "NL-012", Name = "Syrup đường mía nguyên chất", Unit = "ml", StockQty = 24000, MinThreshold = 3000 };
        var ingBrownSugar = new Ingredient { Code = "NL-013", Name = "Sốt đường đen Hàn Quốc", Unit = "ml", StockQty = 12000, MinThreshold = 2000 };
        var ingWhipping = new Ingredient { Code = "NL-014", Name = "Kem béo Whipping Cream", Unit = "ml", StockQty = 750, MinThreshold = 1200 }; // Cảnh báo sắp hết!
        var ingFreshOrange = new Ingredient { Code = "NL-015", Name = "Cam sành tươi ép", Unit = "g", StockQty = 25000, MinThreshold = 4000 };
        var ingFreshFruit = new Ingredient { Code = "NL-016", Name = "Trái cây tươi tổng hợp (Bơ/Xoài/Dưa hấu)", Unit = "g", StockQty = 28000, MinThreshold = 4000 };
        var ingTaroPaste = new Ingredient { Code = "NL-017", Name = "Sốt khoai môn tươi nghiền", Unit = "g", StockQty = 6500, MinThreshold = 1000 };
        var ingBakeryUnit = new Ingredient { Code = "NL-018", Name = " Cốt bánh tươi nướng trong ngày", Unit = "phần", StockQty = 220, MinThreshold = 30 };

        var allIngredients = new[]
        {
            ingRobusta, ingArabica, ingCondensedMilk, ingFreshMilk, ingCoconutCream,
            ingBlackTea, ingOolongTea, ingJasmineTea, ingMatcha, ingPeachSlice,
            ingLychee, ingSugarSyrup, ingBrownSugar, ingWhipping, ingFreshOrange,
            ingFreshFruit, ingTaroPaste, ingBakeryUnit
        };
        db.Ingredients.AddRange(allIngredients);
        await db.SaveChangesAsync();

        foreach (var ing in allIngredients)
        {
            db.StockTransactions.Add(new StockTransaction
            {
                IngredientId = ing.Id,
                Type = StockTransactionType.Import,
                Quantity = ing.StockQty,
                BalanceAfter = ing.StockQty,
                UnitCost = 0,
                Reason = "Tồn kho đầu kỳ khởi tạo hệ thống",
                CreatedBy = "System Seeder",
                CreatedAt = DateTime.UtcNow.AddDays(-32)
            });
        }
        await db.SaveChangesAsync();

        // Helper tạo món + size + topping + công thức
        Product AddDrink(Category cat, string name, string desc, string img, decimal priceS, decimal priceM, decimal priceL,
            string liquidHex, bool featured, Topping[] toppings, (Ingredient Ing, decimal Qty)[] recipeBase,
            bool allowTemp = true, bool allowSugar = true, bool allowIce = true)
        {
            var p = new Product
            {
                CategoryId = cat.Id,
                Name = name,
                Description = desc,
                ImageUrl = $"/images/seed/{img}",
                BasePrice = priceS,
                AllowTemperature = allowTemp,
                AllowSugar = allowSugar,
                AllowIce = allowIce,
                IsAvailable = true,
                IsFeatured = featured,
                LiquidColorHex = liquidHex,
                CreatedAt = DateTime.UtcNow.AddDays(-30)
            };
            p.Sizes.Add(new ProductSize { SizeName = "S", Price = priceS, DisplayOrder = 1 });
            p.Sizes.Add(new ProductSize { SizeName = "M", Price = priceM, DisplayOrder = 2 });
            p.Sizes.Add(new ProductSize { SizeName = "L", Price = priceL, DisplayOrder = 3 });

            foreach (var t in toppings)
            {
                p.ProductToppings.Add(new ProductTopping { ToppingId = t.Id });
            }

            foreach (var (ing, qty) in recipeBase)
            {
                p.Recipes.Add(new Recipe { IngredientId = ing.Id, SizeName = null, Quantity = qty });
            }

            db.Products.Add(p);
            return p;
        }

        Product AddBakery(Category cat, string name, string desc, string img, decimal priceStandard, decimal priceBox, string hex, bool featured)
        {
            var p = new Product
            {
                CategoryId = cat.Id,
                Name = name,
                Description = desc,
                ImageUrl = $"/images/seed/{img}",
                BasePrice = priceStandard,
                AllowTemperature = false,
                AllowSugar = false,
                AllowIce = false,
                IsAvailable = true,
                IsFeatured = featured,
                LiquidColorHex = hex,
                CreatedAt = DateTime.UtcNow.AddDays(-30)
            };
            p.Sizes.Add(new ProductSize { SizeName = "Phần tiêu chuẩn", Price = priceStandard, DisplayOrder = 1 });
            p.Sizes.Add(new ProductSize { SizeName = "Combo 2 phần", Price = priceBox, DisplayOrder = 2 });
            p.Recipes.Add(new Recipe { IngredientId = ingBakeryUnit.Id, SizeName = "Phần tiêu chuẩn", Quantity = 1 });
            p.Recipes.Add(new Recipe { IngredientId = ingBakeryUnit.Id, SizeName = "Combo 2 phần", Quantity = 2 });
            db.Products.Add(p);
            return p;
        }

        var coffeeToppings = new[] { topEspresso, topCoffeeJelly, topMacchiato, topOatmilk };
        var teaToppings = new[] { topWhiteBoba, topPeach, topMacchiato };
        var milkTeaToppings = new[] { topBlackBoba, topWhiteBoba, topMacchiato, topCoffeeJelly };
        var juiceToppings = new[] { topWhiteBoba, topPeach };

        // 22 Sản phẩm chất lượng cao
        var products = new List<Product>
        {
            // Cà phê (6 món)
            AddDrink(catCoffee, "Cà Phê Đen Đá Mộc", "Chiết xuất phin truyền thống từ hạt Robusta Đắk Lắk rang mộc đậm đà, hậu vị ngọt sâu.", "ca-phe-den.svg",
                29000, 35000, 42000, "#24130B", true, coffeeToppings,
                new[] { (ingRobusta, 25m), (ingSugarSyrup, 10m) }),
            AddDrink(catCoffee, "Cà Phê Sữa Đá Sài Gòn", "Sự hòa quyện kinh điển giữa cà phê phin đậm đặc và sữa đặc béo thơm chuẩn vị Sài Gòn.", "ca-phe-sua.svg",
                32000, 39000, 45000, "#6F4E37", true, coffeeToppings,
                new[] { (ingRobusta, 25m), (ingCondensedMilk, 30m) }),
            AddDrink(catCoffee, "Bạc Xỉu Cốt Dừa Đạt Mộc", "Sữa tươi quyện cốt dừa béo ngậy phủ lớp kem cà phê đánh bông thơm lừng.", "bac-xiu.svg",
                39000, 45000, 52000, "#B88B64", true, coffeeToppings,
                new[] { (ingRobusta, 15m), (ingFreshMilk, 80m), (ingCoconutCream, 30m), (ingCondensedMilk, 25m) }),
            AddDrink(catCoffee, "Espresso Nguyên Bản Cầu Đất", "100% hạt Arabica Cầu Đất chiết xuất áp suất cao với lớp crema vàng óng mịn màng.", "espresso.svg",
                35000, 42000, 49000, "#2B170E", false, new[] { topEspresso, topOatmilk },
                new[] { (ingArabica, 18m) }, allowIce: false),
            AddDrink(catCoffee, "Caramel Latte Nghệ Thuật", "Espresso Arabica êm dịu hòa cùng sữa tươi đánh nóng mịn như nhung và sốt caramel.", "latte.svg",
                45000, 52000, 59000, "#9C7355", true, coffeeToppings,
                new[] { (ingArabica, 18m), (ingFreshMilk, 150m), (ingSugarSyrup, 15m) }),
            AddDrink(catCoffee, "Cold Brew Cam Sả Thanh Mát", "Cà phê ủ lạnh suốt 18 giờ kết hợp nước cam vàng tươi và hương sả dịu nhẹ.", "cold-brew.svg",
                49000, 55000, 62000, "#5A2D0C", false, new[] { topPeach, topWhiteBoba },
                new[] { (ingArabica, 20m), (ingFreshOrange, 60m), (ingSugarSyrup, 15m) }, allowTemp: false),

            // Trà trái cây (4 món)
            AddDrink(catTea, "Trà Đào Cam Sả Đạt Mộc", "Hồng trà ủ thơm quyện cam vàng tươi, sả cây đập dập và miếng đào ngâm giòn ngọt.", "tra-dao-cam-sa.svg",
                42000, 49000, 55000, "#D97706", true, teaToppings,
                new[] { (ingBlackTea, 150m), (ingPeachSlice, 40m), (ingFreshOrange, 40m), (ingSugarSyrup, 20m) }),
            AddDrink(catTea, "Trà Vải Hoa Hồng Đà Lạt", "Lục trà thanh khiết kết hợp trái vải mọng nước và hương cánh hoa hồng tự nhiên.", "tra-vai-hoa-hong.svg",
                42000, 49000, 55000, "#E11D48", false, teaToppings,
                new[] { (ingJasmineTea, 150m), (ingLychee, 45m), (ingSugarSyrup, 20m) }),
            AddDrink(catTea, "Trà Ô Long Sen Vàng Macchiato", "Trà Ô Long Bảo Lộc thanh tao, hạt sen bùi bở phủ lớp kem phô mai mặn ngọt.", "tra-sen-vang.svg",
                45000, 52000, 59000, "#B45309", true, teaToppings,
                new[] { (ingOolongTea, 160m), (ingWhipping, 35m), (ingSugarSyrup, 15m) }),
            AddDrink(catTea, "Lục Trà Hoa Nhài Nguyên Lá", "Trà xanh ướp hoa nhài tự nhiên vị chát nhẹ đầu lưỡi, hậu ngọt thanh tinh tế.", "tra-xanh-nhai.svg",
                35000, 42000, 48000, "#84CC16", false, teaToppings,
                new[] { (ingJasmineTea, 180m), (ingSugarSyrup, 20m) }),

            // Trà sữa (4 món)
            AddDrink(catMilkTea, "Trà Sữa Trân Châu Đường Đen", "Hồng trà sữa béo mịn vân sốt đường đen Hàn Quốc cùng trân châu dẻo ấm.", "tra-sua-tran-chau.svg",
                45000, 52000, 59000, "#B08968", true, milkTeaToppings,
                new[] { (ingBlackTea, 120m), (ingFreshMilk, 100m), (ingBrownSugar, 30m) }),
            AddDrink(catMilkTea, "Trà Sữa Ô Long Rang Đậm Vị", "Trà Ô Long rang lửa vừa dậy hương khói nhẹ quyện sữa tươi thanh trùng.", "tra-sua-oolong.svg",
                45000, 52000, 58000, "#A47551", false, milkTeaToppings,
                new[] { (ingOolongTea, 140m), (ingFreshMilk, 90m), (ingCondensedMilk, 20m) }),
            AddDrink(catMilkTea, "Matcha Latte Uji Kem Mịn", "Bột trà xanh Matcha Uji Nhật Bản đánh bông thủ công cùng sữa tươi thơm ngậy.", "matcha-latte.svg",
                49000, 55000, 62000, "#4D7C0F", true, milkTeaToppings,
                new[] { (ingMatcha, 12m), (ingFreshMilk, 150m), (ingSugarSyrup, 20m) }),
            AddDrink(catMilkTea, "Trà Sữa Khoai Môn Tươi Nghiền", "Khoai môn tươi hấp chín nghiền mịn kết hợp trà lài sữa ngọt dịu tự nhiên.", "tra-sua-khoai-mon.svg",
                49000, 55000, 62000, "#9333EA", false, milkTeaToppings,
                new[] { (ingJasmineTea, 100m), (ingFreshMilk, 100m), (ingTaroPaste, 55m) }),

            // Nước ép & Sinh tố (4 món)
            AddDrink(catJuice, "Nước Ép Cam Vàng Nguyên Chất", "Ép tươi trực tiếp từ cam sành chín mọng giàu Vitamin C không pha nước.", "nuoc-ep-cam.svg",
                42000, 49000, 55000, "#FB923C", false, juiceToppings,
                new[] { (ingFreshOrange, 250m), (ingSugarSyrup, 10m) }, allowTemp: false),
            AddDrink(catJuice, "Nước Ép Dưa Hấu Bạc Hà", "Dưa hấu đỏ ép lạnh giải nhiệt tức thì điểm lá bạc hà the mát.", "nuoc-ep-dua-hau.svg",
                39000, 45000, 52000, "#DC2626", false, juiceToppings,
                new[] { (ingFreshFruit, 240m), (ingSugarSyrup, 10m) }, allowTemp: false),
            AddDrink(catJuice, "Sinh Tố Bơ Sáp Đắk Lắk", "Bơ sáp dẻo quánh xay cùng sữa đặc và đá tuyết mịn tan đầu lưỡi.", "sinh-to-bo.svg",
                49000, 55000, 65000, "#84CC16", false, Array.Empty<Topping>(),
                new[] { (ingFreshFruit, 180m), (ingCondensedMilk, 35m), (ingFreshMilk, 60m) }, allowTemp: false),
            AddDrink(catJuice, "Sinh Tố Xoài Cát Hòa Lộc", "Xoài cát chín thơm ngọt tự nhiên xay mịn cung cấp năng lượng tươi mới.", "sinh-to-xoai.svg",
                49000, 55000, 65000, "#FACC15", false, juiceToppings,
                new[] { (ingFreshFruit, 180m), (ingCondensedMilk, 25m), (ingFreshMilk, 50m) }, allowTemp: false),

            // Bánh ngọt (4 món)
            AddBakery(catBakery, "Bánh Sừng Bò Bơ Pháp (Croissant)", "Vỏ bánh ngàn lớp giòn rụm thơm nức bơ lạt Pháp nướng mới mỗi sáng.", "croissant.svg",
                35000, 65000, "#D97706", true),
            AddBakery(catBakery, "Tiramisu Cà Phê Ý Thủ Công", "Lớp kem phô mai Mascarpone béo nhẹ xen cốt bánh thấm đẫm rượu Rum & Espresso.", "tiramisu.svg",
                45000, 85000, "#5D4037", true),
            AddBakery(catBakery, "Chanh Dây Phô Mai (Cheesecake)", "Đế bánh quy bơ giòn cùng phô mai mịn mượt phủ sốt chanh dây chua thanh.", "cheesecake.svg",
                45000, 85000, "#FBBF24", false),
            AddBakery(catBakery, "Bánh Mì Que Pate Cột Đèn", "Bánh mì que nướng giòn nóng hổi nhân pate gan béo bùi và sốt bơ cay nhẹ.", "banh-mi-que.svg",
                25000, 45000, "#EA580C", false)
        };

        await db.SaveChangesAsync();

        // 14 Bàn chia 3 khu vực với tọa độ không gian 3D Three.js chuẩn xác
        var tables = new List<CafeTable>
        {
            // Tầng 1 (6 bàn: x từ -5.5 đến -0.5, z/PosY từ -4.0 đến +4.0)
            new() { Code = "T1-01", Zone = "Tầng 1", Capacity = 2, PosX = -5.5, PosY = -3.2 },
            new() { Code = "T1-02", Zone = "Tầng 1", Capacity = 4, PosX = -2.2, PosY = -3.2 },
            new() { Code = "T1-03", Zone = "Tầng 1", Capacity = 4, PosX = -5.5, PosY = 0.2 },
            new() { Code = "T1-04", Zone = "Tầng 1", Capacity = 6, PosX = -2.2, PosY = 0.2 },
            new() { Code = "T1-05", Zone = "Tầng 1", Capacity = 2, PosX = -5.5, PosY = 3.5 },
            new() { Code = "T1-06", Zone = "Tầng 1", Capacity = 4, PosX = -2.2, PosY = 3.5 },

            // Tầng 2 (4 bàn: x từ +1.5 đến +5.0, PosY từ -3.5 đến +0.2)
            new() { Code = "T2-01", Zone = "Tầng 2", Capacity = 4, PosX = 2.0, PosY = -3.2 },
            new() { Code = "T2-02", Zone = "Tầng 2", Capacity = 6, PosX = 5.4, PosY = -3.2 },
            new() { Code = "T2-03", Zone = "Tầng 2", Capacity = 2, PosX = 2.0, PosY = 0.2 },
            new() { Code = "T2-04", Zone = "Tầng 2", Capacity = 4, PosX = 5.4, PosY = 0.2 },

            // Sân vườn (4 bàn: x từ +1.5 đến +5.8, PosY từ +3.2 đến +6.2)
            new() { Code = "SV-01", Zone = "Sân vườn", Capacity = 4, PosX = 2.0, PosY = 3.6 },
            new() { Code = "SV-02", Zone = "Sân vườn", Capacity = 6, PosX = 5.4, PosY = 3.6 },
            new() { Code = "SV-03", Zone = "Sân vườn", Capacity = 4, PosX = 2.0, PosY = 6.5 },
            new() { Code = "SV-04", Zone = "Sân vườn", Capacity = 8, PosX = 5.4, PosY = 6.5 }
        };
        db.CafeTables.AddRange(tables);
        await db.SaveChangesAsync();

        // 3 Khuyến mãi: 1 phần trăm đang chạy, 1 số tiền có đơn tối thiểu đang chạy, 1 đã hết hạn (Mục 11)
        var now = DateTime.UtcNow;
        db.Promotions.AddRange(
            new Promotion
            {
                Code = "DATMOC15",
                Name = "Ưu đãi 15% thưởng thức hương mộc",
                Type = PromotionType.Percentage,
                Value = 15,
                MaxDiscount = 30000,
                MinOrderValue = 60000,
                StartAt = now.AddDays(-15),
                EndAt = now.AddDays(45),
                UsageLimit = 500,
                PerUserLimit = 10,
                IsActive = true
            },
            new Promotion
            {
                Code = "CHAOBAN20K",
                Name = "Giảm ngay 20.000 ₫ cho đơn từ 100.000 ₫",
                Type = PromotionType.FixedAmount,
                Value = 20000,
                MaxDiscount = null,
                MinOrderValue = 100000,
                StartAt = now.AddDays(-10),
                EndAt = now.AddDays(60),
                UsageLimit = 300,
                PerUserLimit = 5,
                IsActive = true
            },
            new Promotion
            {
                Code = "HE2025",
                Name = "Khuyến mãi chào hè (Đã kết thúc)",
                Type = PromotionType.Percentage,
                Value = 20,
                MaxDiscount = 40000,
                MinOrderValue = 80000,
                StartAt = now.AddDays(-90),
                EndAt = now.AddDays(-30),
                UsageLimit = 200,
                PerUserLimit = 2,
                IsActive = true
            }
        );
        await db.SaveChangesAsync();

        // 1 Đặt bàn mẫu trong 20 phút tới để hiển thị bàn "Đã đặt trước" trên sơ đồ 3D
        db.TableReservations.Add(new TableReservation
        {
            TableId = tables[7].Id, // T2-02
            CustomerName = "Chị Ngọc Ánh (Họp nhóm)",
            Phone = "0909888777",
            PartySize = 5,
            ReservedAt = DateTime.UtcNow.AddMinutes(20),
            Status = ReservationStatus.Confirmed,
            Note = "Nhờ chuẩn bị ổ cắm điện cho laptop"
        });
        tables[7].Status = TableStatus.Reserved;
        await db.SaveChangesAsync();

        // 5. Đơn hàng demo (60 đơn rải trong 30 ngày qua bằng chính OrderService theo Mục 11!)
        bool seedDemoOrders = env.IsDevelopment() && config.GetValue<bool>("Seed:DemoOrders", true);
        if (seedDemoOrders)
        {
            logger.LogInformation("Đang sinh 60 đơn hàng demo rải trong 30 ngày qua qua OrderService...");
            var orderService = services.GetRequiredService<IOrderService>();
            var rng = new Random(20261102);

            // Tránh dùng quá nhiều nguyên liệu đang dưới ngưỡng (Arabica, Đào, Whipping) trong 60 đơn lịch sử để không cạn về 0
            var safeDemoProducts = products
                .Where(p => !p.Name.Contains("Espresso") && !p.Name.Contains("Latte") && !p.Name.Contains("Cold Brew") &&
                            !p.Name.Contains("Trà Đào") && !p.Name.Contains("Sen Vàng"))
                .ToList();

            string[] guestNames =
            {
                "Lê Hoàng Nam", "Phạm Minh Tuấn", "Nguyễn Thu Hà", "Trần Quốc Bảo",
                "Võ Thanh Trúc", "Đặng Gia Huy", "Bùi Khánh Linh", "Hoàng Đức Anh"
            };

            for (int i = 0; i < 56; i++)
            {
                int daysAgo = rng.Next(0, 30);
                int hourVn = rng.Next(8, 21);
                int min = rng.Next(0, 59);
                var createdUtc = DateTime.UtcNow.Date.AddDays(-daysAgo).AddHours(hourVn - 7).AddMinutes(min);
                if (createdUtc > DateTime.UtcNow.AddMinutes(-10))
                    createdUtc = DateTime.UtcNow.AddMinutes(-30);

                bool isPos = rng.NextDouble() < 0.55;
                var staff = (i % 3 == 0 && staff2User != null) ? staff2User : staff1User;
                var picked1 = safeDemoProducts[rng.Next(safeDemoProducts.Count)];
                var picked2 = safeDemoProducts[rng.Next(safeDemoProducts.Count)];

                var lines = new List<OrderRequestLineDto>
                {
                    new()
                    {
                        ProductId = picked1.Id,
                        SizeName = picked1.Sizes.ElementAt(rng.Next(picked1.Sizes.Count)).SizeName,
                        Quantity = rng.Next(1, 3),
                        Temperature = picked1.AllowTemperature ? "Lạnh" : null,
                        SugarLevel = picked1.AllowSugar ? "70%" : null,
                        IceLevel = picked1.AllowIce ? "100%" : null
                    }
                };
                if (rng.NextDouble() < 0.6)
                {
                    lines.Add(new OrderRequestLineDto
                    {
                        ProductId = picked2.Id,
                        SizeName = picked2.Sizes.First().SizeName,
                        Quantity = 1,
                        Temperature = picked2.AllowTemperature ? "Lạnh" : null,
                        SugarLevel = picked2.AllowSugar ? "100%" : null,
                        IceLevel = picked2.AllowIce ? "70%" : null
                    });
                }

                var createReq = new CreateOrderRequestDto
                {
                    CustomerId = (!isPos && customer1User != null && i % 2 == 0) ? customer1User.Id : null,
                    StaffId = staff?.Id,
                    ActorDisplayName = staff?.FullName ?? guestNames[i % guestNames.Length],
                    OrderType = isPos ? OrderType.TakeAway : OrderType.TakeAway,
                    Source = isPos ? OrderSource.POS : OrderSource.Online,
                    PaymentMethod = "CASH",
                    MarkPaidImmediately = isPos,
                    CustomerName = (!isPos && customer1User != null && i % 2 == 0) ? customer1User.FullName : guestNames[i % guestNames.Length],
                    Phone = "0903123456",
                    PromoCode = (i % 9 == 0) ? "DATMOC15" : null,
                    OverrideCreatedAtUtc = createdUtc,
                    Lines = lines
                };

                var (ok, created, _) = await orderService.CreateOrderAsync(createReq);
                if (!ok || created == null) continue;

                string actor = staff?.FullName ?? "Admin";

                // 50 đơn hoàn thành, 6 đơn bị hủy để đa dạng biểu đồ trạng thái
                if (i % 10 == 9)
                {
                    await orderService.CancelOrderAsync(created.Id, "Khách đổi ý chọn món khác", actor, null, isAdmin: true, hasStaffCancelClaim: true);
                }
                else
                {
                    if (created.Status == OrderStatus.Pending)
                        await orderService.ChangeStatusAsync(created.Id, OrderStatus.Confirmed, actor, isAdmin: true);

                    await orderService.ChangeStatusAsync(created.Id, OrderStatus.Brewing, actor, isAdmin: true);
                    await orderService.ChangeStatusAsync(created.Id, OrderStatus.Ready, actor, isAdmin: true);
                    await orderService.ConfirmPaymentAsync(created.Id, actor);
                    await orderService.ChangeStatusAsync(created.Id, OrderStatus.Completed, actor, isAdmin: true);

                    // Giữ mốc thời gian lịch sử để biểu đồ 30 ngày phân bổ tự nhiên
                    var tracked = await db.Orders.FirstAsync(o => o.Id == created.Id);
                    tracked.CreatedAt = createdUtc;
                    tracked.UpdatedAt = createdUtc.AddMinutes(18);
                    tracked.PaidAt = createdUtc.AddMinutes(15);
                    await db.SaveChangesAsync();
                }
            }

            // Tạo thêm 4 đơn đang hoạt động thực tế ngay lúc này (2 đơn mở tại bàn để sơ đồ 3D có khách ngồi + tách cà phê bốc khói, 2 đơn online mới!)
            var actorNow = staff1User?.FullName ?? "Ca trưởng";

            // Đơn bàn T1-02 (Đang pha chế)
            var (_, t1Order, _) = await orderService.CreateOrderAsync(new CreateOrderRequestDto
            {
                StaffId = staff1User?.Id,
                ActorDisplayName = actorNow,
                TableId = tables[1].Id, // T1-02
                OrderType = OrderType.DineIn,
                Source = OrderSource.POS,
                PaymentMethod = "CASH",
                MarkPaidImmediately = false,
                CustomerName = "Bàn T1-02 (Anh Minh)",
                Lines = new List<OrderRequestLineDto>
                {
                    new() { ProductId = products[1].Id, SizeName = "M", Quantity = 2, Temperature = "Lạnh", SugarLevel = "70%", IceLevel = "100%" },
                    new() { ProductId = products[18].Id, SizeName = "Phần tiêu chuẩn", Quantity = 1 }
                }
            });
            if (t1Order != null)
            {
                await orderService.ChangeStatusAsync(t1Order.Id, OrderStatus.Brewing, actorNow, isAdmin: false);
            }

            // Đơn bàn SV-01 Sân vườn (Sẵn sàng nhận - khách đang ngồi thưởng thức chờ thanh toán)
            var (_, svOrder, _) = await orderService.CreateOrderAsync(new CreateOrderRequestDto
            {
                StaffId = staff1User?.Id,
                ActorDisplayName = actorNow,
                TableId = tables[10].Id, // SV-01
                OrderType = OrderType.DineIn,
                Source = OrderSource.POS,
                PaymentMethod = "CASH",
                MarkPaidImmediately = false,
                CustomerName = "Bàn SV-01 (Nhóm bạn Lan)",
                Lines = new List<OrderRequestLineDto>
                {
                    new() { ProductId = products[10].Id, SizeName = "L", Quantity = 2, ToppingIds = new List<int> { topBlackBoba.Id } },
                    new() { ProductId = products[19].Id, SizeName = "Phần tiêu chuẩn", Quantity = 2 }
                }
            });
            if (svOrder != null)
            {
                await orderService.ChangeStatusAsync(svOrder.Id, OrderStatus.Brewing, actorNow, isAdmin: false);
                await orderService.ChangeStatusAsync(svOrder.Id, OrderStatus.Ready, actorNow, isAdmin: false);
            }

            // 2 Đơn Online của Customer đang chờ xác nhận
            await orderService.CreateOrderAsync(new CreateOrderRequestDto
            {
                CustomerId = customer1User?.Id,
                ActorDisplayName = customer1User?.FullName ?? "Lê Hoàng Nam",
                OrderType = OrderType.TakeAway,
                Source = OrderSource.Online,
                PaymentMethod = "CASH",
                CustomerName = customer1User?.FullName ?? "Lê Hoàng Nam",
                Phone = "0938112233",
                Note = "Lấy ống hút giấy giúp mình nhé!",
                Lines = new List<OrderRequestLineDto>
                {
                    new() { ProductId = products[2].Id, SizeName = "M", Quantity = 1, Temperature = "Lạnh", SugarLevel = "50%", IceLevel = "70%" },
                    new() { ProductId = products[12].Id, SizeName = "L", Quantity = 1, ToppingIds = new List<int> { topWhiteBoba.Id } }
                }
            });

            await orderService.CreateOrderAsync(new CreateOrderRequestDto
            {
                CustomerId = customer1User?.Id,
                ActorDisplayName = customer1User?.FullName ?? "Lê Hoàng Nam",
                OrderType = OrderType.Delivery,
                Source = OrderSource.Online,
                PaymentMethod = "CASH",
                CustomerName = customer1User?.FullName ?? "Lê Hoàng Nam",
                Phone = "0938112233",
                DeliveryAddress = "45 Lê Lợi, Phường Bến Nghé, Quận 1, TP.HCM",
                PromoCode = "CHAOBAN20K",
                Lines = new List<OrderRequestLineDto>
                {
                    new() { ProductId = products[1].Id, SizeName = "L", Quantity = 2 },
                    new() { ProductId = products[19].Id, SizeName = "Phần tiêu chuẩn", Quantity = 1 }
                }
            });
        }

        logger.LogInformation("Hoàn tất khởi tạo dữ liệu mẫu Đạt Mộc Coffee.");
    }

    private static async Task<ApplicationUser> EnsureUserAsync(
        UserManager<ApplicationUser> userManager,
        AppDbContext db,
        string email,
        string password,
        string fullName,
        string phone,
        string role,
        string? employeePos,
        List<string>? permissions,
        string? address = null)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user == null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = fullName,
                Phone = phone,
                PhoneNumber = phone,
                Address = address,
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddDays(-35)
            };

            var createRes = await userManager.CreateAsync(user, password);
            if (!createRes.Succeeded)
            {
                var errs = string.Join("; ", createRes.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Không thể tạo tài khoản seed {email}: {errs}");
            }
        }

        if (!await userManager.IsInRoleAsync(user, role))
        {
            await userManager.AddToRoleAsync(user, role);
        }

        if (!string.IsNullOrWhiteSpace(employeePos))
        {
            var hasProfile = await db.EmployeeProfiles.AnyAsync(p => p.UserId == user.Id);
            if (!hasProfile)
            {
                db.EmployeeProfiles.Add(new EmployeeProfile
                {
                    UserId = user.Id,
                    Position = employeePos,
                    HireDate = DateTime.UtcNow.AddMonths(-6),
                    WorkStatus = "Đang làm"
                });
                await db.SaveChangesAsync();
            }
        }

        if (permissions != null && permissions.Count > 0)
        {
            var existingClaims = await userManager.GetClaimsAsync(user);
            foreach (var perm in permissions)
            {
                if (!existingClaims.Any(c => c.Type == StaffPermissions.ClaimType && c.Value == perm))
                {
                    await userManager.AddClaimAsync(user, new Claim(StaffPermissions.ClaimType, perm));
                }
            }
        }

        return user;
    }
}

