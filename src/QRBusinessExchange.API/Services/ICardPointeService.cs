using Qrbound.Payments.Models;

namespace QRBusinessExchange.API.Services;

/// <summary>Charges tokenized cards through the CardPointe gateway.</summary>
public interface ICardPointeService
{
    /// <summary>
    /// Authorizes and captures (a sale) a tokenized card. Never throws for gateway problems:
    /// declines, timeouts and configuration errors come back as a result with IsSuccess false.
    /// </summary>
    Task<CardPointeResponse> ChargeAsync(CardPointeChargeRequest request, CancellationToken cancellationToken = default);

    /// <summary>Settings the browser needs for the card form. Holds no secrets.</summary>
    PaymentConfig GetPublicConfig();
}

/// <summary>A sale on a tokenized card. Raw card numbers and CVVs are not accepted.</summary>
public sealed class CardPointeChargeRequest
{
    /// <summary>Token from the hosted iFrame tokenizer; sent to CardPointe as "account".</summary>
    public required string Token { get; init; }

    /// <summary>Card expiry as MMYY.</summary>
    public required string Expiry { get; init; }

    /// <summary>Amount in cents.</summary>
    public required long AmountCents { get; init; }

    public required string Currency { get; init; }

    /// <summary>Your invoice/order number; sent as "orderid" (lets you look the charge up after a timeout).</summary>
    public required string OrderId { get; init; }

    public string? CardholderName { get; init; }

    public string? Email { get; init; }
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
