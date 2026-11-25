using Qrbound.Payments.Models;

namespace QRBusinessExchange.API.Services;

/// <summary>Charges tokenized cards through the CardPointe gateway, and keeps cards on file there.</summary>
public interface ICardPointeService
{
    /// <summary>
    /// Authorizes and captures (a sale) a tokenized card, or a card on file when the request names a
    /// profile. Never throws for gateway problems: declines, timeouts and configuration errors come
    /// back as a result with IsSuccess false.
    /// </summary>
    Task<CardPointeResponse> ChargeAsync(CardPointeChargeRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores a tokenized card in CardPointe's profile service (card on file). Only the returned
    /// profile and account IDs are kept by this app.
    /// </summary>
    Task<CardPointeProfileResult> CreateProfileAsync(CardPointeProfileRequest request, CancellationToken cancellationToken = default);

    /// <summary>Removes a card on file from CardPointe. Returns false if CardPointe didn't confirm.</summary>
    Task<bool> DeleteProfileAsync(string profileId, string accountId, CancellationToken cancellationToken = default);

    /// <summary>Settings the browser needs for the card form. Holds no secrets.</summary>
    PaymentConfig GetPublicConfig();
}

/// <summary>
/// A sale on a tokenized card (Token and Expiry) or on a card on file (ProfileId and AccountId).
/// Raw card numbers and CVVs are not accepted.
/// </summary>
public sealed class CardPointeChargeRequest
{
    /// <summary>Token from the hosted iFrame tokenizer; sent to CardPointe as "account".</summary>
    public string? Token { get; init; }

    /// <summary>Card expiry as MMYY. Needed with Token.</summary>
    public string? Expiry { get; init; }

    /// <summary>CardPointe profile of a card on file; use instead of Token and Expiry.</summary>
    public string? ProfileId { get; init; }

    /// <summary>The account inside the profile (acctid).</summary>
    public string? AccountId { get; init; }

    /// <summary>Amount in cents.</summary>
    public required long AmountCents { get; init; }

    public required string Currency { get; init; }

    /// <summary>Your invoice/order number; sent as "orderid" (lets you look the charge up after a timeout).</summary>
    public required string OrderId { get; init; }

    public string? CardholderName { get; init; }

    public string? Email { get; init; }

    public bool UsesProfile => !string.IsNullOrWhiteSpace(ProfileId);
}

/// <summary>CardPointe's answer, reduced to what the app uses.</summary>
public sealed class CardPointeResponse
{
    public bool IsSuccess { get; init; }

    /// <summary>"Approved", "Declined", "Retry" or "Error".</summary>
    public string StatusMessage { get; init; } = "Error";

    /// <summary>Retrieval reference (retref).</summary>
    public string? TransactionId { get; init; }

    public string? AuthorizationCode { get; init; }

    /// <summary>Message safe to show to the payer.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// True when CardPointe never answered (timeout): the card may or may not have been charged.
    /// Look the order up in CardPointe before charging again.
    /// </summary>
    public bool OutcomeUnknown { get; init; }
}

/// <summary>A tokenized card to keep on file.</summary>
public sealed class CardPointeProfileRequest
{
    public required string Token { get; init; }

    /// <summary>MMYY.</summary>
    public required string Expiry { get; init; }

    public string? CardholderName { get; init; }
}

/// <summary>The card on file CardPointe created.</summary>
public sealed class CardPointeProfileResult
{
    public bool IsSuccess { get; init; }
    public string? ProfileId { get; init; }
    public string? AccountId { get; init; }

    /// <summary>Last four digits (CardSecure tokens keep the card's last four).</summary>
    public string? Last4 { get; init; }

    /// <summary>e.g. VISA, MC, AMEX, DISC.</summary>
    public string? CardBrand { get; init; }

    /// <summary>Message safe to show to the user.</summary>
    public string? ErrorMessage { get; init; }
}
