using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace CoffeeShop.Models.Entities;

public class ApplicationUser : IdentityUser
{
    [Required, MaxLength(120)]
    public string FullName { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? Phone { get; set; }

    [MaxLength(300)]
    public string? Address { get; set; }

    [MaxLength(300)]
    public string? AvatarUrl { get; set; }

    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; } = false;
    public bool MustChangePassword { get; set; } = false;
    public int LoyaltyPoints { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public EmployeeProfile? EmployeeProfile { get; set; }
    public ICollection<Order> CustomerOrders { get; set; } = new List<Order>();
    public ICollection<Order> HandledOrders { get; set; } = new List<Order>();
}

public class EmployeeProfile
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    [Required, MaxLength(100)]
    public string Position { get; set; } = "Barista / Thu ngân";

    public DateTime HireDate { get; set; } = DateTime.UtcNow;

    [Required, MaxLength(50)]
    public string WorkStatus { get; set; } = "Đang làm"; // Đang làm / Nghỉ phép / Đã nghỉ
}

