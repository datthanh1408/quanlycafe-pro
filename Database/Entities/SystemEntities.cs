using System.ComponentModel.DataAnnotations;

namespace CoffeeShop.Models.Entities;

public class Shift
{
    public int Id { get; set; }

    [Required]
    public string StaffId { get; set; } = string.Empty;
    public ApplicationUser Staff { get; set; } = null!;

    public DateTime OpenedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAt { get; set; }

    public decimal OpeningCash { get; set; }
    public decimal ExpectedCash { get; set; }
    public decimal? CountedCash { get; set; }
    public decimal? Difference { get; set; }

    [MaxLength(200)]
    public string? Note { get; set; }

    public ICollection<Order> Orders { get; set; } = new List<Order>();
}

public class AuditLog
{
    public int Id { get; set; }

    [MaxLength(120)]
    public string? UserId { get; set; }

    [MaxLength(120)]
    public string? UserEmail { get; set; }

    [Required, MaxLength(100)]
    public string Action { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string EntityName { get; set; } = string.Empty;

    [MaxLength(80)]
    public string? EntityId { get; set; }

    public string? OldValues { get; set; }
    public string? NewValues { get; set; }

    [MaxLength(60)]
    public string? IpAddress { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class SystemSetting
{
    [Key, MaxLength(100)]
    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Description { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class PaymentMethodConfig
{
    public int Id { get; set; }

    [Required, MaxLength(30)]
    public string Code { get; set; } = string.Empty; // CASH, QR, CARD

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; } = 0;
}

