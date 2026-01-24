using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QRBusinessExchange.Core.Entities;

/// <summary>
/// Represents a payment transaction with geolocation verification
/// </summary>
public class Payment
{
    [Key]
    public Guid PaymentId { get; set; } = Guid.NewGuid();

    [Required]
    public Guid UserId { get; set; }

    [Required]
    [Column(TypeName = "decimal(10, 2)")]
    public decimal Amount { get; set; }

    [Required]
    [StringLength(3)]
    public string Currency { get; set; } = "USD";

    [Required]
    [StringLength(50)]
    public string PaymentGateway { get; set; } = string.Empty; // PayPal/Stripe/Clover

    [StringLength(255)]
    public string? TransactionId { get; set; }

    [Required]
    [StringLength(20)]
    public string Status { get; set; } = "Pending"; // Pending/Completed/Failed/Rejected

    /// <summary>
    /// GPS coordinates at payment initiation
    /// </summary>
    [Column(TypeName = "decimal(10, 8)")]
    public decimal? PaymentLatitude { get; set; }

    [Column(TypeName = "decimal(10, 8)")]
    public decimal? PaymentLongitude { get; set; }

    /// <summary>
    /// GPS coordinates at payment verification
    /// </summary>
    [Column(TypeName = "decimal(10, 8)")]
    public decimal? VerificationLatitude { get; set; }

    [Column(TypeName = "decimal(11, 8)")]
    public decimal? VerificationLongitude { get; set; }

    /// <summary>
    /// Distance between payment and verification locations in yards
    /// </summary>
    [Column(TypeName = "decimal(10, 2)")]
    public decimal? DistanceVerification { get; set; }

    public bool IsVerified { get; set; } = false;

    [StringLength(500)]
    public string? RejectionReason { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public virtual User User { get; set; } = null!;
}
