using System.ComponentModel.DataAnnotations;

namespace QRBusinessExchange.Core.Entities;

/// <summary>
/// How a business takes payments. Each business sets its own; only its Owners and Admins can change
/// them. A business without a row has payments turned off.
/// </summary>
public class BusinessPaymentSettings
{
    [Key]
    public Guid BusinessId { get; set; }

    /// <summary>Off until an Owner or Admin turns payments on.</summary>
    public bool PaymentsEnabled { get; set; }

    /// <summary>Smallest amount the business accepts, in cents; null uses the platform minimum.</summary>
    public long? MinAmountCents { get; set; }

    /// <summary>Largest amount the business accepts, in cents; null uses the platform maximum.</summary>
    public long? MaxAmountCents { get; set; }

    /// <summary>How long a new QR code can be paid, in minutes (5 to 1440).</summary>
    public int DefaultExpiresInMinutes { get; set; } = 30;

    /// <summary>Whether signed-in customers may pay with a saved card and their fingerprint.</summary>
    public bool AllowSavedCards { get; set; } = true;

    /// <summary>True: only Owners and Admins can take payments. False: any member can.</summary>
    public bool ManagersOnly { get; set; }

    /// <summary>Whether customers can add a tip when they pay.</summary>
    public bool TipsEnabled { get; set; }

    /// <summary>Suggested tips in percent, comma-separated, e.g. "15,18,20" (at most 4).</summary>
    [StringLength(50)]
    public string TipPercentages { get; set; } = "15,18,20";

    /// <summary>Whether customers can type their own tip amount.</summary>
    public bool AllowCustomTip { get; set; } = true;

    /// <summary>The largest tip accepted, in percent of the bill (1 to 100).</summary>
    public int MaxTipPercent { get; set; } = 50;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Guid? UpdatedByUserId { get; set; }

    public virtual Business Business { get; set; } = null!;
}
