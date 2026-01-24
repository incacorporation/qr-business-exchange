using System.ComponentModel.DataAnnotations;

namespace QRBusinessExchange.Core.DTOs;

/// <summary>
/// DTO for creating a payment request
/// </summary>
public class PaymentRequestDto
{
    /// <summary>
    /// Payment amount
    /// </summary>
    [Required(ErrorMessage = "Amount is required")]
    [Range(0.01, 999999.99, ErrorMessage = "Amount must be between 0.01 and 999999.99")]
    public decimal Amount { get; set; }

    /// <summary>
    /// Payment gateway to use (PayPal, Stripe, or Clover)
    /// </summary>
    [Required(ErrorMessage = "Payment gateway is required")]
    [StringLength(50)]
    [RegularExpression("^(PayPal|Stripe|Clover)$", ErrorMessage = "Payment gateway must be PayPal, Stripe, or Clover")]
    public string PaymentGateway { get; set; } = string.Empty;

    /// <summary>
    /// Subscription type being purchased
    /// </summary>
    [Required(ErrorMessage = "Subscription type is required")]
    [StringLength(50)]
    [RegularExpression("^(Free|Basic|Premium|Enterprise)$", ErrorMessage = "Subscription type must be Free, Basic, Premium, or Enterprise")]
    public string SubscriptionType { get; set; } = string.Empty;
}
