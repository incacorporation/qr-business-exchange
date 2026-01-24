using System.ComponentModel.DataAnnotations;

namespace QRBusinessExchange.Core.Entities;

/// <summary>
/// Represents a user's subscription tier and credit allocation
/// </summary>
public class Subscription
{
    [Key]
    public Guid SubscriptionId { get; set; } = Guid.NewGuid();

    [Required]
    public Guid UserId { get; set; }

    [Required]
    [StringLength(50)]
    public string SubscriptionType { get; set; } = "Free"; // Free/Basic/Premium/Enterprise

    public int CreditsAvailable { get; set; } = 0;

    public int CreditsUsed { get; set; } = 0;

    [Required]
    public DateTime StartDate { get; set; } = DateTime.UtcNow;

    public DateTime? EndDate { get; set; }

    public bool AutoRenew { get; set; } = true;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public virtual User User { get; set; } = null!;
}
