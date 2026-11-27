using System.ComponentModel.DataAnnotations;

namespace QRBusinessExchange.Core.DTOs;

/// <summary>A card on file, as its owner sees it.</summary>
public class SavedCardDto
{
    public Guid Id { get; set; }
    public string Last4 { get; set; } = string.Empty;

    /// <summary>e.g. VISA, MC, AMEX, DISC.</summary>
    public string? CardBrand { get; set; }

    /// <summary>MMYY.</summary>
    public string Expiry { get; set; } = string.Empty;

    public string? CardholderName { get; set; }
    public bool IsExpired { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
}

/// <summary>Save a card tokenized by CardPointe's iFrame tokenizer.</summary>
public class SaveCardRequest
{
    [Required, StringLength(64, MinimumLength = 4)]
    public string PaymentToken { get; set; } = string.Empty;

    /// <summary>MMYY.</summary>
    [Required, RegularExpression(@"^(0[1-9]|1[0-2])\d{2}$", ErrorMessage = "Expiry must be MMYY.")]
    public string Expiry { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string CardholderName { get; set; } = string.Empty;
}

/// <summary>Start paying a QR payment request with a saved card: asks for the passkey options.</summary>
public class SavedCardPayInitRequest
{
    public Guid SavedCardId { get; set; }

    /// <summary>The quote (bill plus tip) the fingerprint approves. Null approves the bill with no tip.</summary>
    public Guid? QuoteId { get; set; }
}

/// <summary>Pay a QR payment request with a saved card, confirmed by the user's passkey (fingerprint / face).</summary>
public class SavedCardPayRequest
{
    public Guid SavedCardId { get; set; }

    /// <summary>The challenge from the init call's options.</summary>
    [Required]
    public string Challenge { get; set; } = string.Empty;

    [Required]
    public AssertionResponseDto Assertion { get; set; } = new();

    /// <summary>Optional receipt email.</summary>
    [EmailAddress, StringLength(254)]
    public string? Email { get; set; }

    /// <summary>The same quote as the init call.</summary>
    public Guid? QuoteId { get; set; }
}
