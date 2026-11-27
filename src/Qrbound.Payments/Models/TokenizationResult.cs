namespace Qrbound.Payments.Models;

/// <summary>
/// What CardPointe's hosted iFrame tokenizer posts back to the page
/// (its message, expiry and validationError fields).
/// </summary>
public sealed class TokenizationResult
{
    /// <summary>The card token ("message" in the tokenizer's post).</summary>
    public string? Token { get; set; }

    /// <summary>Card expiry as YYYYMM, when the tokenizer runs with useexpiry=true.</summary>
    public string? Expiry { get; set; }

    /// <summary>Set when the card number did not validate; Token is empty then.</summary>
    public string? ValidationError { get; set; }

    /// <summary>True when a token came back and nothing failed validation.</summary>
    public bool IsValid => !string.IsNullOrEmpty(Token) && string.IsNullOrEmpty(ValidationError);
}
