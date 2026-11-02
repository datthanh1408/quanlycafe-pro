using System.ComponentModel.DataAnnotations;

namespace CoffeeShop.Models.Entities;

public class Category
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(120)]
    public string Slug { get; set; } = string.Empty;

    [MaxLength(300)]
    public string? ImageUrl { get; set; }

    [MaxLength(80)]
    public string IconClass { get; set; } = "bi-cup-hot";

    public int DisplayOrder { get; set; } = 0;
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; } = false;

    public ICollection<Product> Products { get; set; } = new List<Product>();
}

public class Product
{
    public int Id { get; set; }

    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;

    [Required, MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(300)]
    public string ImageUrl { get; set; } = "/images/seed/default-drink.svg";

    public decimal BasePrice { get; set; }

    public bool AllowTemperature { get; set; } = true;
    public bool AllowSugar { get; set; } = true;
    public bool AllowIce { get; set; } = true;

    public bool IsAvailable { get; set; } = true;
    public bool IsFeatured { get; set; } = false;
    public int SoldCount { get; set; } = 0;
    public bool IsDeleted { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Màu chất lỏng hiển thị cho mô hình Three.js 3D Drink Customizer
    [MaxLength(20)]
    public string LiquidColorHex { get; set; } = "#4A2C18";

    public ICollection<ProductSize> Sizes { get; set; } = new List<ProductSize>();
    public ICollection<ProductTopping> ProductToppings { get; set; } = new List<ProductTopping>();
    public ICollection<Recipe> Recipes { get; set; } = new List<Recipe>();
}

public class ProductSize
{
    public int Id { get; set; }

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    [Required, MaxLength(20)]
    public string SizeName { get; set; } = "M"; // S, M, L

    public decimal Price { get; set; }
    public int DisplayOrder { get; set; } = 0;
}

public class Topping
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public decimal Price { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; } = false;

    public ICollection<ProductTopping> ProductToppings { get; set; } = new List<ProductTopping>();
}

public class ProductTopping
{
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public int ToppingId { get; set; }
    public Topping Topping { get; set; } = null!;
}

