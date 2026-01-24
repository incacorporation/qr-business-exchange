using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QRBusinessExchange.Core.Entities;

/// <summary>
/// Represents an advertisement banner created by paid subscribers
/// </summary>
public class Advertisement
{
    [Key]
    public Guid AdId { get; set; } = Guid.NewGuid();

    [Required]
    public Guid UserId { get; set; }

    public string? AdContent { get; set; }

    [StringLength(500)]
    public string? ImageUrl { get; set; }

    [Required]
    public DateTime StartDate { get; set; }

    [Required]
    public DateTime EndDate { get; set; }

    [Column(TypeName = "decimal(10, 2)")]
    public decimal CostPerDay { get; set; } = 5.00m;

    [Required]
    [Column(TypeName = "decimal(10, 2)")]
    public decimal TotalCost { get; set; }

    public bool IsActive { get; set; } = true;

    public int Impressions { get; set; } = 0;

    public int Clicks { get; set; } = 0;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public virtual User User { get; set; } = null!;
}
