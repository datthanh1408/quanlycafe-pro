using System.Threading.RateLimiting;
using CoffeeShop.Authorization;
using CoffeeShop.Data;
using CoffeeShop.Data.Seed;
using CoffeeShop.Hubs;
using CoffeeShop.Models.Entities;
using CoffeeShop.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    WebRootPath = "Frontend/wwwroot"
});

// Cấu hình WebRoot và Razor View Engine cho cấu trúc 3 phần (Database - Backend - Frontend)
builder.WebHost.UseWebRoot("Frontend/wwwroot");

builder.Services.Configure<Microsoft.AspNetCore.Mvc.Razor.RazorViewEngineOptions>(options =>
{
    options.ViewLocationFormats.Clear();
    options.ViewLocationFormats.Add("/Frontend/Views/{1}/{0}.cshtml");
    options.ViewLocationFormats.Add("/Frontend/Views/Shared/{0}.cshtml");

    options.AreaViewLocationFormats.Clear();
    options.AreaViewLocationFormats.Add("/Frontend/Views/Areas/{2}/{1}/{0}.cshtml");
    options.AreaViewLocationFormats.Add("/Frontend/Views/Areas/{2}/Shared/{0}.cshtml");
    options.AreaViewLocationFormats.Add("/Frontend/Views/Shared/{0}.cshtml");
});

// 1. Database (EF Core + SQL Server LocalDB)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Chưa cấu hình chuỗi kết nối 'DefaultConnection'.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

// 2. ASP.NET Core Identity + Chính sách mật khẩu & Khóa tạm 5 phút sau 5 lần sai (Mục 4.2 & 5.1)
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequiredLength = 8;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireDigit = true;
    options.Password.RequireNonAlphanumeric = false;

    options.User.RequireUniqueEmail = true;

    options.Lockout.AllowedForNewUsers = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "DatMocCoffee.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.LoginPath = "/login";
    options.LogoutPath = "/logout";
    options.AccessDeniedPath = "/error/403";
    options.ExpireTimeSpan = TimeSpan.FromHours(12);
    options.SlidingExpiration = true;
});

// 3. Phân quyền theo Role & Permission Claims (Mục 5.2 & 5.3)
builder.Services.AddAuthorization(options =>
{
    AppPolicies.RegisterPolicies(options);
});

// 4. Rate Limiting chống brute-force đăng nhập (Mục 10)
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("LoginLimiter", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "local",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

// 5. Session lưu giỏ hàng giữ lại trước & sau khi đăng nhập (Mục 3.3)
builder.Services.AddMemoryCache();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = "DatMocCoffee.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.IdleTimeout = TimeSpan.FromHours(6);
});

// 6. Anti-Forgery cho cả Form và AJAX (Mục 10)
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "RequestVerificationToken";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddSignalR();
builder.Services.AddControllersWithViews();

// 7. Đăng ký toàn bộ Business Services qua Dependency Injection (Mục 3.3)
builder.Services.AddScoped<ISettingService, SettingService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IPricingService, PricingService>();
builder.Services.AddScoped<IPromotionService, PromotionService>();
builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<ITableService, TableService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IShiftService, ShiftService>();
builder.Services.AddScoped<IReportService, ReportService>();

var app = builder.Build();

// 8. Trang lỗi thân thiện 403 / 404 / 500 không lộ stack trace (Mục 3.3 & 10)
app.UseExceptionHandler("/error/500");
app.UseStatusCodePagesWithReExecute("/error/{0}");

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

// 9. Security Headers cơ bản (Mục 10)
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    await next();
});

app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapHub<OrderHub>("/hubs/order");

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// 10. Tự động áp dụng Migration & Seed dữ liệu khởi tạo
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var env = services.GetRequiredService<IWebHostEnvironment>();
    var config = services.GetRequiredService<IConfiguration>();
    await DbInitializer.InitializeAsync(services, env, config);
}

app.Run();
