namespace QRBusinessExchange.Core.Interfaces;

/// <summary>
/// Service interface for payment processing operations across multiple gateways
/// </summary>
public interface IPaymentService
{
    /// <summary>
    /// Creates a PayPal payment order
    /// </summary>
    /// <param name="userId">The unique identifier of the user making the payment</param>
    /// <param name="amount">The payment amount</param>
    /// <param name="currency">The currency code (e.g., USD)</param>
    /// <param name="subscriptionType">The subscription type being purchased</param>
    /// <returns>The PayPal order ID and approval URL</returns>
    Task<(string OrderId, string ApprovalUrl)> CreatePayPalPaymentAsync(Guid userId, decimal amount, string currency, string subscriptionType);

    /// <summary>
    /// Captures a PayPal payment after user approval
    /// </summary>
    /// <param name="orderId">The PayPal order ID</param>
    /// <returns>True if capture was successful, false otherwise</returns>
    Task<bool> CapturePayPalPaymentAsync(string orderId);

    /// <summary>
    /// Creates a Stripe payment intent
    /// </summary>
    /// <param name="userId">The unique identifier of the user making the payment</param>
    /// <param name="amount">The payment amount in cents</param>
    /// <param name="currency">The currency code (e.g., USD)</param>
    /// <param name="subscriptionType">The subscription type being purchased</param>
    /// <returns>The Stripe client secret for payment confirmation</returns>
    Task<string> CreateStripePaymentAsync(Guid userId, decimal amount, string currency, string subscriptionType);

    /// <summary>
    /// Confirms a Stripe payment intent
    /// </summary>
    /// <param name="paymentIntentId">The Stripe payment intent ID</param>
    /// <returns>True if confirmation was successful, false otherwise</returns>
    Task<bool> ConfirmStripePaymentAsync(string paymentIntentId);

    /// <summary>
    /// Creates a Clover payment
    /// </summary>
    /// <param name="userId">The unique identifier of the user making the payment</param>
    /// <param name="amount">The payment amount</param>
    /// <param name="currency">The currency code (e.g., USD)</param>
    /// <param name="subscriptionType">The subscription type being purchased</param>
    /// <returns>The Clover payment ID and status</returns>
    Task<(string PaymentId, string Status)> CreateCloverPaymentAsync(Guid userId, decimal amount, string currency, string subscriptionType);

    /// <summary>
    /// Verifies that payment and verification locations are within acceptable distance
    /// </summary>
    /// <param name="paymentId">The unique identifier of the payment</param>
    /// <param name="verificationLatitude">The latitude at verification time</param>
    /// <param name="verificationLongitude">The longitude at verification time</param>
    /// <returns>True if verification passes, false otherwise</returns>
    Task<bool> VerifyPaymentLocationAsync(Guid paymentId, decimal verificationLatitude, decimal verificationLongitude);
}
