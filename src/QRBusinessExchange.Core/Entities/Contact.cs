using System.ComponentModel.DataAnnotations;

namespace QRBusinessExchange.Core.Entities;

/// <summary>
/// A person saved to a user's contacts after scanning their business card (QR code).
/// One-sided, like keeping a paper business card: no credit is used and the other person isn't asked.
/// </summary>
public class Contact
{
    [Key]
    public Guid ContactId { get; set; } = Guid.NewGuid();

    /// <summary>The user whose contact list this entry is in.</summary>
    [Required]
    public Guid OwnerUserId { get; set; }

    /// <summary>The person whose card was scanned.</summary>
    [Required]
    public Guid ContactUserId { get; set; }

    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual User OwnerUser { get; set; } = null!;
    public virtual User ContactUser { get; set; } = null!;
}
