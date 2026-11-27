using System.ComponentModel.DataAnnotations;

namespace QRBusinessExchange.Core.DTOs;

/// <summary>A business's payment settings, with the platform limits they must fit inside.</summary>
public class BusinessPaymentSettingsDto
{
    public Guid BusinessId { get; set; }
    public bool PaymentsEnabled { get; set; }
    public long? MinAmountCents { get; set; }
    public long? MaxAmountCents { get; set; }
    public int DefaultExpiresInMinutes { get; set; } = 30;
    public bool AllowSavedCards { get; set; } = true;
    public bool ManagersOnly { get; set; }

    public bool TipsEnabled { get; set; }

    /// <summary>Suggested tips in percent, e.g. "15, 18, 20".</summary>
    public string TipPercentages { get; set; } = "15, 18, 20";

    public bool AllowCustomTip { get; set; } = true;
    public int MaxTipPercent { get; set; } = 50;

    /// <summary>The limits that apply right now (the business's own, inside the platform's).</summary>
    public long EffectiveMinAmountCents { get; set; }
    public long EffectiveMaxAmountCents { get; set; }

    public long PlatformMinAmountCents { get; set; }
    public long PlatformMaxAmountCents { get; set; }
    public string Currency { get; set; } = "USD";

    /// <summary>Whether the signed-in user can change these settings (Owner, Admin or SuperAdmin).</summary>
    public bool CanEdit { get; set; }

    /// <summary>Whether the signed-in user can take payments under these settings.</summary>
    public bool CanTakePayments { get; set; }

    public DateTime? UpdatedAt { get; set; }
}

/// <summary>Change a business's payment settings.</summary>
public class UpdateBusinessPaymentSettingsRequest
{
    public bool PaymentsEnabled { get; set; }

    [Range(1, long.MaxValue)]
    public long? MinAmountCents { get; set; }

    [Range(1, long.MaxValue)]
    public long? MaxAmountCents { get; set; }

    [Range(5, 1440)]
    public int DefaultExpiresInMinutes { get; set; } = 30;

    public bool AllowSavedCards { get; set; } = true;

    public bool ManagersOnly { get; set; }

    /// <summary>Whether customers can add a tip when they pay.</summary>
    public bool TipsEnabled { get; set; }

    /// <summary>Suggested tips in percent, comma-separated: up to four of 1 to 100, e.g. "15, 18, 20".</summary>
    [StringLength(50)]
    public string? TipPercentages { get; set; } = "15, 18, 20";

    /// <summary>Whether customers can type their own tip amount.</summary>
    public bool AllowCustomTip { get; set; } = true;

    /// <summary>The largest tip accepted, in percent of the bill.</summary>
    [Range(1, 100)]
    public int MaxTipPercent { get; set; } = 50;
}

/// <summary>A business's QR payments over a period.</summary>
public class QrPaymentReportDto
{
    /// <summary>First day included (UTC date).</summary>
    public DateTime From { get; set; }

    /// <summary>Last day included (UTC date).</summary>
    public DateTime To { get; set; }

    public string Currency { get; set; } = "USD";
    public int PaidCount { get; set; }

    /// <summary>What was charged: bills plus tips.</summary>
    public long PaidTotalCents { get; set; }

    /// <summary>The paid bills without tips.</summary>
    public long BillTotalCents { get; set; }

    public long TipTotalCents { get; set; }

    /// <summary>Paid with a card typed into CardPointe's fields on qrbound's pay page.</summary>
    public int PaidByCardCount { get; set; }

    /// <summary>Paid with a saved card and the customer's fingerprint.</summary>
    public int PaidBySavedCardCount { get; set; }

    /// <summary>Paid on Fiserv's CardPointe Hosted Payment Page.</summary>
    public int PaidOnHostedPageCount { get; set; }

    // Payments made before the method was recorded aren't in any of these three counts.

    public int OpenCount { get; set; }
    public int ExpiredCount { get; set; }
    public int CancelledCount { get; set; }
    public int LockedCount { get; set; }
    public int NeedsReviewCount { get; set; }

    /// <summary>Paid totals per UTC day, oldest first; days without payments are left out.</summary>
    public List<QrPaymentReportDayDto> Days { get; set; } = new();
}

public class QrPaymentReportDayDto
{
    public DateTime Date { get; set; }
    public int PaidCount { get; set; }

    /// <summary>Bills plus tips.</summary>
    public long PaidTotalCents { get; set; }

    public long TipTotalCents { get; set; }
}
