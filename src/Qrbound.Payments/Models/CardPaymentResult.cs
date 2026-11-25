namespace Qrbound.Payments.Models;

/// <summary>The outcome of a card charge, as the server returns it to the browser.</summary>
public sealed class CardPaymentResult
{
    /// <summary>True when CardPointe approved the charge.</summary>
    public bool IsSuccess { get; set; }

    /// <summary>CardPointe retrieval reference (retref); use it to look up, void or refund the charge.</summary>
    public string? TransactionId { get; set; }

    /// <summary>Authorization code from the card issuer.</summary>
    public string? AuthorizationCode { get; set; }

    /// <summary>"Approved", "Declined", "Retry" or "Error".</summary>
    public string StatusMessage { get; set; } = string.Empty;

    /// <summary>Why the charge did not go through, safe to show to the payer.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Amount charged, in cents.</summary>
    public long AmountCents { get; set; }

    /// <summary>When the server got the gateway's answer (UTC).</summary>
    public DateTime TransactionDateTime { get; set; } = DateTime.UtcNow;
}
