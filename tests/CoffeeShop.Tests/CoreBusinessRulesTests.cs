using CoffeeShop.Data;
using CoffeeShop.Hubs;
using CoffeeShop.Models.Entities;
using CoffeeShop.Models.Enums;
using CoffeeShop.Models.ViewModels;
using CoffeeShop.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CoffeeShop.Tests;

public class CoreBusinessRulesTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly AppDbContext _db;
    private readonly SettingService _settingService;
    private readonly AuditService _auditService;
    private readonly PricingService _pricingService;
    private readonly PromotionService _promotionService;
    private readonly InventoryService _inventoryService;
    private readonly OrderService _orderService;

    public CoreBusinessRulesTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();

        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_conn)
            .Options;

        _db = new AppDbContext(opts);
        _db.Database.EnsureCreated();

        _settingService = new SettingService(_db, new MemoryCache(new MemoryCacheOptions()));
        _auditService = new AuditService(_db, new HttpContextAccessor());
        _pricingService = new PricingService(_db, _settingService);
        _promotionService = new PromotionService(_db);
        _inventoryService = new InventoryService(_db, _auditService);
        _orderService = new OrderService(
            _db,
            _pricingService,
            _promotionService,
            _inventoryService,
            _settingService,
            _auditService,
            new FakeHubContext(),
            NullLogger<OrderService>.Instance);

        SeedBaseFixture();
    }

    private void SeedBaseFixture()
    {
        var cat = new Category { Id = 1, Name = "Cà phê", Slug = "ca-phe", IsActive = true };
        var top1 = new Topping { Id = 1, Name = "Trân châu đen", Price = 8000, IsActive = true };
        var top2 = new Topping { Id = 2, Name = "Kem Macchiato", Price = 12000, IsActive = true };

        var prod = new Product
        {
            Id = 1,
            CategoryId = 1,
            Name = "Cà Phê Sữa Đá",
            BasePrice = 32000,
            IsAvailable = true
        };
        prod.Sizes.Add(new ProductSize { SizeName = "S", Price = 32000, DisplayOrder = 1 });
        prod.Sizes.Add(new ProductSize { SizeName = "M", Price = 39000, DisplayOrder = 2 });
        prod.Sizes.Add(new ProductSize { SizeName = "L", Price = 45000, DisplayOrder = 3 });
        prod.ProductToppings.Add(new ProductTopping { ProductId = 1, ToppingId = 1 });
        prod.ProductToppings.Add(new ProductTopping { ProductId = 1, ToppingId = 2 });

        var ingCoffee = new Ingredient { Id = 1, Code = "NL-CF", Name = "Hạt cà phê", Unit = "g", StockQty = 60, MinThreshold = 20, IsActive = true };
        var ingMilk = new Ingredient { Id = 2, Code = "NL-MILK", Name = "Sữa đặc", Unit = "ml", StockQty = 100, MinThreshold = 30, IsActive = true };

        prod.Recipes.Add(new Recipe { ProductId = 1, IngredientId = 1, Quantity = 25 }); // 25g / ly
        prod.Recipes.Add(new Recipe { ProductId = 1, IngredientId = 2, Quantity = 30 }); // 30ml / ly

        var table = new CafeTable { Id = 1, Code = "T1-01", Zone = "Tầng 1", Capacity = 4, Status = TableStatus.Available, IsActive = true };

        var promoPercent = new Promotion
        {
            Id = 1,
            Code = "GIAM15",
            Name = "Giảm 15% tối đa 20k",
            Type = PromotionType.Percentage,
            Value = 15,
            MaxDiscount = 20000,
            MinOrderValue = 50000,
            StartAt = DateTime.UtcNow.AddDays(-1),
            EndAt = DateTime.UtcNow.AddDays(5),
            UsageLimit = 10,
            PerUserLimit = 2,
            IsActive = true
        };

        var promoExpired = new Promotion
        {
            Id = 2,
            Code = "HETHAN",
            Name = "Mã hết hạn",
            Type = PromotionType.FixedAmount,
            Value = 15000,
            MinOrderValue = 0,
            StartAt = DateTime.UtcNow.AddDays(-10),
            EndAt = DateTime.UtcNow.AddDays(-2),
            IsActive = true
        };

        _db.Categories.Add(cat);
        _db.Toppings.AddRange(top1, top2);
        _db.Ingredients.AddRange(ingCoffee, ingMilk);
        _db.Products.Add(prod);
        _db.CafeTables.Add(table);
        _db.Promotions.AddRange(promoPercent, promoExpired);
        _db.SaveChanges();
    }

    [Fact]
    public async Task PricingService_CalculatesSizePlusToppingsTimesQuantity_AndShippingThreshold()
    {
        // Size M (39.000) + Trân châu (8.000) + Kem (12.000) = 59.000 x 2 = 118.000 ₫
        var lines = await _pricingService.PriceLinesAsync(new[]
        {
            ("L1", new OrderRequestLineDto
            {
                ProductId = 1,
                SizeName = "M",
                ToppingIds = new List<int> { 1, 2 },
                Quantity = 2
            })
        });

        Assert.Single(lines);
        Assert.Equal(59000m, lines[0].UnitPrice);
        Assert.Equal(118000m, lines[0].LineTotal);

        var shipUnder150k = await _pricingService.CalculateShippingFeeAsync(OrderType.Delivery, 118000m);
        var shipOver150k = await _pricingService.CalculateShippingFeeAsync(OrderType.Delivery, 160000m);
        Assert.Equal(15000m, shipUnder150k);
        Assert.Equal(0m, shipOver150k);
    }

    [Fact]
    public async Task PromotionService_EnforcesMinOrder_MaxCap_AndRejectsExpired()
    {
        var underMin = await _promotionService.ValidateAndCalculateAsync("GIAM15", 40000m, null);
        Assert.False(underMin.IsValid);

        // 200.000 * 15% = 30.000 -> bị trần MaxDiscount 20.000 ₫
        var capped = await _promotionService.ValidateAndCalculateAsync("GIAM15", 200000m, null);
        Assert.True(capped.IsValid);
        Assert.Equal(20000m, capped.DiscountAmount);

        var expired = await _promotionService.ValidateAndCalculateAsync("HETHAN", 100000m, null);
        Assert.False(expired.IsValid);
    }

    [Fact]
    public async Task OrderLifecycle_EnforcesStepByStep_DeductsInventoryOnBrewing_RequiresPaidBeforeCompleted()
    {
        // Tạo đơn Online 2 ly Size M (cần 2 * 25g = 50g cà phê, kho đang có 60g)
        var (ok, order, err) = await _orderService.CreateOrderAsync(new CreateOrderRequestDto
        {
            TableId = 1,
            OrderType = OrderType.DineIn,
            Source = OrderSource.Online,
            CustomerName = "Khách Test",
            Lines = new List<OrderRequestLineDto>
            {
                new() { ProductId = 1, SizeName = "M", Quantity = 2 }
            }
        });

        Assert.True(ok, err);
        Assert.NotNull(order);
        Assert.Equal(OrderStatus.Pending, order!.Status);

        // 1. Chặn nhảy cóc từ Pending sang Brewing
        var jumpRes = await _orderService.ChangeStatusAsync(order.Id, OrderStatus.Brewing, "Staff", isAdmin: false);
        Assert.False(jumpRes.Success);

        // 2. Chuyển Pending -> Confirmed -> Brewing (tự động trừ kho 50g, còn 10g!)
        Assert.True((await _orderService.ChangeStatusAsync(order.Id, OrderStatus.Confirmed, "Staff", false)).Success);
        Assert.True((await _orderService.ChangeStatusAsync(order.Id, OrderStatus.Brewing, "Staff", false)).Success);

        var coffeeAfter = await _db.Ingredients.AsNoTracking().FirstAsync(i => i.Id == 1);
        Assert.Equal(10m, coffeeAfter.StockQty);

        // 3. Brewing -> Ready
        Assert.True((await _orderService.ChangeStatusAsync(order.Id, OrderStatus.Ready, "Staff", false)).Success);

        // 4. Chặn chuyển sang Completed khi chưa thanh toán
        var unpaidComplete = await _orderService.ChangeStatusAsync(order.Id, OrderStatus.Completed, "Staff", false);
        Assert.False(unpaidComplete.Success);

        // 5. Xác nhận thu tiền -> Hoàn thành -> Bàn tự động về Trống
        Assert.True((await _orderService.ConfirmPaymentAsync(order.Id, "Staff")).Success);
        Assert.True((await _orderService.ChangeStatusAsync(order.Id, OrderStatus.Completed, "Staff", false)).Success);

        var tableAfter = await _db.CafeTables.AsNoTracking().FirstAsync(t => t.Id == 1);
        Assert.Equal(TableStatus.Available, tableAfter.Status);
    }

    [Fact]
    public async Task InventoryService_BlocksNegativeStock_WhenIngredientsInsufficient()
    {
        // Kho chỉ có 60g cà phê, đặt 3 ly (cần 75g) -> Tạo đơn POS (Confirmed) thành công nhưng chặn lúc bấm chuyển sang Brewing
        var (ok, order, _) = await _orderService.CreateOrderAsync(new CreateOrderRequestDto
        {
            OrderType = OrderType.TakeAway,
            Source = OrderSource.POS,
            CustomerName = "Khách POS",
            Lines = new List<OrderRequestLineDto>
            {
                new() { ProductId = 1, SizeName = "S", Quantity = 3 }
            }
        });

        Assert.True(ok);
        var brewAttempt = await _orderService.ChangeStatusAsync(order!.Id, OrderStatus.Brewing, "Staff", false);
        Assert.False(brewAttempt.Success);
        Assert.Contains("Hạt cà phê", brewAttempt.ErrorMessage);

        var stockUnchanged = await _db.Ingredients.AsNoTracking().FirstAsync(i => i.Id == 1);
        Assert.Equal(60m, stockUnchanged.StockQty);
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }

    private sealed class FakeHubContext : IHubContext<OrderHub>
    {
        public IHubClients Clients { get; } = new FakeHubClients();
        public IGroupManager Groups => throw new NotImplementedException();
    }

    private sealed class FakeHubClients : IHubClients
    {
        private readonly IClientProxy _proxy = new FakeClientProxy();
        public IClientProxy All => _proxy;
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => _proxy;
        public IClientProxy Client(string connectionId) => _proxy;
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => _proxy;
        public IClientProxy Group(string groupName) => _proxy;
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => _proxy;
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => _proxy;
        public IClientProxy User(string userId) => _proxy;
        public IClientProxy Users(IReadOnlyList<string> userIds) => _proxy;
    }

    private sealed class FakeClientProxy : IClientProxy
    {
        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}

