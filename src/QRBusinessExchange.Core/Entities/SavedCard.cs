using System.ComponentModel.DataAnnotations;

namespace QRBusinessExchange.Core.Entities;

/// <summary>
/// A card a user keeps on file for fingerprint checkout. The card itself lives in CardPointe's
/// profile service; this row holds only the profile reference and what's needed to show it
/// ("VISA ending 1111, expires 12/28").
/// </summary>
public class SavedCard
{
    [Key]
    public Guid SavedCardId { get; set; } = Guid.NewGuid();

    [Required]
    public Guid UserId { get; set; }

    /// <summary>CardPointe profile ID.</summary>
    [Required]
    [StringLength(30)]
    public string ProfileId { get; set; } = string.Empty;

    /// <summary>The account inside the profile (CardPointe acctid).</summary>
    [Required]
    [StringLength(10)]
    public string AccountId { get; set; } = string.Empty;

    [Required]
    [StringLength(4)]
    public string Last4 { get; set; } = string.Empty;

    /// <summary>e.g. VISA, MC, AMEX, DISC.</summary>
    [StringLength(20)]
    public string? CardBrand { get; set; }

    /// <summary>MMYY.</summary>
    [Required]
    [StringLength(4)]
    public string Expiry { get; set; } = string.Empty;

    [StringLength(100)]
    public string? CardholderName { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastUsedAt { get; set; }

    public virtual User User { get; set; } = null!;

    public const int MaxPerUser = 5;

    /// <summary>True once the card's expiry month is over.</summary>
    public bool IsExpiredOn(DateTime utcNow)
    {
        if (Expiry.Length != 4 || !int.TryParse(Expiry[..2], out var month) || !int.TryParse(Expiry[2..], out var year))
        {
            return true;
        }

        var firstOfNextMonth = new DateTime(2000 + year, month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1);
        return utcNow >= firstOfNextMonth;
    }
}
