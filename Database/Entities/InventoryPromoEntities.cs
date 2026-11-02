using System.ComponentModel.DataAnnotations;
using CoffeeShop.Models.Enums;

namespace CoffeeShop.Models.Entities;

public class Promotion
{
    public int Id { get; set; }

    [Required, MaxLength(40)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    public PromotionType Type { get; set; } = PromotionType.Percentage;
    public decimal Value { get; set; }
    public decimal? MaxDiscount { get; set; }
    public decimal MinOrderValue { get; set; } = 0;

    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }

    public int UsageLimit { get; set; } = 1000;
    public int PerUserLimit { get; set; } = 5;
    public int UsedCount { get; set; } = 0;

    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; } = false;

    public ICollection<PromotionUsage> Usages { get; set; } = new List<PromotionUsage>();
}

public class PromotionUsage
{
    public int Id { get; set; }

    public int PromotionId { get; set; }
    public Promotion Promotion { get; set; } = null!;

    public string? UserId { get; set; }
    public ApplicationUser? User { get; set; }

    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;

    public DateTime UsedAt { get; set; } = DateTime.UtcNow;
}

public class Ingredient
{
    public int Id { get; set; }

    [Required, MaxLength(30)]
    public string Code { get; set; } = string.Empty; // NL-001

    [Required, MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(30)]
    public string Unit { get; set; } = "g"; // g, ml, cái, hộp

    public decimal StockQty { get; set; } = 0;
    public decimal MinThreshold { get; set; } = 0;

    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; } = false;

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ICollection<Recipe> Recipes { get; set; } = new List<Recipe>();
    public ICollection<StockTransaction> Transactions { get; set; } = new List<StockTransaction>();
}

public class Recipe
{
    public int Id { get; set; }

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    [MaxLength(20)]
    public string? SizeName { get; set; } // null = áp dụng mọi size

    public int IngredientId { get; set; }
    public Ingredient Ingredient { get; set; } = null!;

    public decimal Quantity { get; set; }
}

public class StockTransaction
{
    public int Id { get; set; }

    public int IngredientId { get; set; }
    public Ingredient Ingredient { get; set; } = null!;

    public StockTransactionType Type { get; set; }
    public decimal Quantity { get; set; } // Dương khi nhập, âm khi xuất/bán/hao hụt, hoặc delta điều chỉnh
    public decimal BalanceAfter { get; set; }
    public decimal UnitCost { get; set; } = 0;

    [Required, MaxLength(200)]
    public string Reason { get; set; } = string.Empty;

    public int? OrderId { get; set; }
    public Order? Order { get; set; }

    [Required, MaxLength(120)]
    public string CreatedBy { get; set; } = "System";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

