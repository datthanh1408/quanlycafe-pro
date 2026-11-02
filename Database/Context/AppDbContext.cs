using CoffeeShop.Models.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShop.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<EmployeeProfile> EmployeeProfiles => Set<EmployeeProfile>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductSize> ProductSizes => Set<ProductSize>();
    public DbSet<Topping> Toppings => Set<Topping>();
    public DbSet<ProductTopping> ProductToppings => Set<ProductTopping>();
    public DbSet<CafeTable> CafeTables => Set<CafeTable>();
    public DbSet<TableReservation> TableReservations => Set<TableReservation>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OrderItemTopping> OrderItemToppings => Set<OrderItemTopping>();
    public DbSet<OrderStatusHistory> OrderStatusHistories => Set<OrderStatusHistory>();
    public DbSet<Promotion> Promotions => Set<Promotion>();
    public DbSet<PromotionUsage> PromotionUsages => Set<PromotionUsage>();
    public DbSet<Ingredient> Ingredients => Set<Ingredient>();
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<StockTransaction> StockTransactions => Set<StockTransaction>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<PaymentMethodConfig> PaymentMethods => Set<PaymentMethodConfig>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // EmployeeProfile (1 - 0..1)
        builder.Entity<EmployeeProfile>(e =>
        {
            e.HasIndex(x => x.UserId).IsUnique();
            e.HasOne(x => x.User)
                .WithOne(u => u.EmployeeProfile)
                .HasForeignKey<EmployeeProfile>(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Category
        builder.Entity<Category>(e =>
        {
            e.HasIndex(x => x.Slug).IsUnique();
        });

        // Product
        builder.Entity<Product>(e =>
        {
            e.Property(x => x.BasePrice).HasColumnType("decimal(18,0)");
            e.HasIndex(x => new { x.CategoryId, x.IsDeleted });
            e.HasOne(x => x.Category)
                .WithMany(c => c.Products)
                .HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ProductSize (pure child)
        builder.Entity<ProductSize>(e =>
        {
            e.Property(x => x.Price).HasColumnType("decimal(18,0)");
            e.HasOne(x => x.Product)
                .WithMany(p => p.Sizes)
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Topping & ProductTopping
        builder.Entity<Topping>(e =>
        {
            e.Property(x => x.Price).HasColumnType("decimal(18,0)");
        });

        builder.Entity<ProductTopping>(e =>
        {
            e.HasKey(x => new { x.ProductId, x.ToppingId });
            e.HasOne(x => x.Product)
                .WithMany(p => p.ProductToppings)
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Topping)
                .WithMany(t => t.ProductToppings)
                .HasForeignKey(x => x.ToppingId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // CafeTable (mapped to SQL table "Tables")
        builder.Entity<CafeTable>(e =>
        {
            e.ToTable("Tables");
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.RowVersion).IsRowVersion();
        });

        // TableReservation
        builder.Entity<TableReservation>(e =>
        {
            e.HasOne(x => x.Table)
                .WithMany(t => t.Reservations)
                .HasForeignKey(x => x.TableId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Order
        builder.Entity<Order>(e =>
        {
            e.HasIndex(x => x.Code).IsUnique();
            e.HasIndex(x => new { x.Status, x.CreatedAt });
            e.Property(x => x.Subtotal).HasColumnType("decimal(18,0)");
            e.Property(x => x.DiscountAmount).HasColumnType("decimal(18,0)");
            e.Property(x => x.ShippingFee).HasColumnType("decimal(18,0)");
            e.Property(x => x.Total).HasColumnType("decimal(18,0)");
            e.Property(x => x.RowVersion).IsRowVersion();

            e.HasOne(x => x.Customer)
                .WithMany(u => u.CustomerOrders)
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Staff)
                .WithMany(u => u.HandledOrders)
                .HasForeignKey(x => x.StaffId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Table)
                .WithMany(t => t.Orders)
                .HasForeignKey(x => x.TableId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Promotion)
                .WithMany()
                .HasForeignKey(x => x.PromotionId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Shift)
                .WithMany(s => s.Orders)
                .HasForeignKey(x => x.ShiftId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // OrderItem (pure child of Order, Restrict on Product)
        builder.Entity<OrderItem>(e =>
        {
            e.Property(x => x.UnitPrice).HasColumnType("decimal(18,0)");
            e.Property(x => x.LineTotal).HasColumnType("decimal(18,0)");

            e.HasOne(x => x.Order)
                .WithMany(o => o.Items)
                .HasForeignKey(x => x.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Product)
                .WithMany()
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // OrderItemTopping (pure child)
        builder.Entity<OrderItemTopping>(e =>
        {
            e.Property(x => x.Price).HasColumnType("decimal(18,0)");
            e.HasOne(x => x.OrderItem)
                .WithMany(i => i.Toppings)
                .HasForeignKey(x => x.OrderItemId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // OrderStatusHistory (pure child)
        builder.Entity<OrderStatusHistory>(e =>
        {
            e.HasOne(x => x.Order)
                .WithMany(o => o.StatusHistories)
                .HasForeignKey(x => x.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Promotion & PromotionUsage
        builder.Entity<Promotion>(e =>
        {
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Value).HasColumnType("decimal(18,0)");
            e.Property(x => x.MaxDiscount).HasColumnType("decimal(18,0)");
            e.Property(x => x.MinOrderValue).HasColumnType("decimal(18,0)");
        });

        builder.Entity<PromotionUsage>(e =>
        {
            e.HasOne(x => x.Promotion)
                .WithMany(p => p.Usages)
                .HasForeignKey(x => x.PromotionId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Order)
                .WithMany()
                .HasForeignKey(x => x.OrderId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Ingredient, Recipe, StockTransaction
        builder.Entity<Ingredient>(e =>
        {
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.StockQty).HasColumnType("decimal(18,2)");
            e.Property(x => x.MinThreshold).HasColumnType("decimal(18,2)");
            e.Property(x => x.RowVersion).IsRowVersion();
        });

        builder.Entity<Recipe>(e =>
        {
            e.Property(x => x.Quantity).HasColumnType("decimal(18,2)");
            e.HasOne(x => x.Product)
                .WithMany(p => p.Recipes)
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Ingredient)
                .WithMany(i => i.Recipes)
                .HasForeignKey(x => x.IngredientId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<StockTransaction>(e =>
        {
            e.HasIndex(x => new { x.IngredientId, x.CreatedAt });
            e.Property(x => x.Quantity).HasColumnType("decimal(18,2)");
            e.Property(x => x.BalanceAfter).HasColumnType("decimal(18,2)");
            e.Property(x => x.UnitCost).HasColumnType("decimal(18,0)");

            e.HasOne(x => x.Ingredient)
                .WithMany(i => i.Transactions)
                .HasForeignKey(x => x.IngredientId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Order)
                .WithMany()
                .HasForeignKey(x => x.OrderId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Shift
        builder.Entity<Shift>(e =>
        {
            e.Property(x => x.OpeningCash).HasColumnType("decimal(18,0)");
            e.Property(x => x.ExpectedCash).HasColumnType("decimal(18,0)");
            e.Property(x => x.CountedCash).HasColumnType("decimal(18,0)");
            e.Property(x => x.Difference).HasColumnType("decimal(18,0)");

            e.HasOne(x => x.Staff)
                .WithMany()
                .HasForeignKey(x => x.StaffId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // PaymentMethodConfig
        builder.Entity<PaymentMethodConfig>(e =>
        {
            e.HasIndex(x => x.Code).IsUnique();
        });

        // Hỗ trợ chạy Unit Test trên SQLite In-Memory (SQLite không tự sinh kiểu rowversion của SQL Server)
        if (Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true)
        {
            builder.Entity<CafeTable>().Property(x => x.RowVersion).HasDefaultValueSql("randomblob(8)").ValueGeneratedOnAdd();
            builder.Entity<Order>().Property(x => x.RowVersion).HasDefaultValueSql("randomblob(8)").ValueGeneratedOnAdd();
            builder.Entity<Ingredient>().Property(x => x.RowVersion).HasDefaultValueSql("randomblob(8)").ValueGeneratedOnAdd();
        }
    }
}

