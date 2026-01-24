using System.ComponentModel.DataAnnotations;

namespace QRBusinessExchange.Core.Entities;

/// <summary>
/// Represents a QR code for user's business card
/// </summary>
public class QRCode
{
    [Key]
    public Guid QRCodeId { get; set; } = Guid.NewGuid();

    [Required]
    public Guid UserId { get; set; }

    [Required]
    public string QRCodeData { get; set; } = string.Empty;

    [Required]
    [StringLength(500)]
    public string PublicProfileUrl { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int ScansCount { get; set; } = 0;

    // Navigation property
    public virtual User User { get; set; } = null!;
}
