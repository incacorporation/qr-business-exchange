using Microsoft.Extensions.Options;
using QRBusinessExchange.Core.Entities;
using QRBusinessExchange.Core.Interfaces;
using QRBusinessExchange.Infrastructure.Configuration;

namespace QRBusinessExchange.Infrastructure.Services;

/// <summary>
/// Service for orchestrating payment operations across multiple payment gateways
/// </summary>
public class PaymentService : IPaymentService
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly IGeolocationService _geolocationService;
    private readonly PayPalService _payPalService;
    private readonly StripeService _stripeService;
    private readonly CloverService _cloverService;
    private readonly PaymentSettings _paymentSettings;

    /// <summary>
    /// Initializes a new instance of the <see cref="PaymentService"/> class
    /// </summary>
    /// <param name="paymentRepository">The payment repository</param>
    /// <param name="geolocationService">The geolocation service</param>
    /// <param name="paymentSettings">Payment configuration settings</param>
    public PaymentService(
        IPaymentRepository paymentRepository,
        IGeolocationService geolocationService,
        IOptions<PaymentSettings> paymentSettings)
    {
        _paymentRepository = paymentRepository ?? throw new ArgumentNullException(nameof(paymentRepository));
        _geolocationService = geolocationService ?? throw new ArgumentNullException(nameof(geolocationService));
        
        if (paymentSettings == null)
        {
            throw new ArgumentNullException(nameof(paymentSettings));
        }

        _paymentSettings = paymentSettings.Value;
        _payPalService = new PayPalService(paymentSettings);
        _stripeService = new StripeService(paymentSettings);
        _cloverService = new CloverService(paymentSettings);
    }

    /// <inheritdoc/>
    public async Task<(string OrderId, string ApprovalUrl)> CreatePayPalPaymentAsync(
        Guid userId, 
        decimal amount, 
        string currency, 
        string subscriptionType)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User ID cannot be empty", nameof(userId));
        }

        if (amount <= 0)
        {
            throw new ArgumentException("Amount must be greater than zero", nameof(amount));
        }

        if (string.IsNullOrWhiteSpace(currency))
        {
            throw new ArgumentException("Currency cannot be null or empty", nameof(currency));
        }

        try
        {
            // Create payment record in database
            var payment = new Payment
            {
                UserId = userId,
                Amount = amount,
                Currency = currency.ToUpper(),
                PaymentGateway = "PayPal",
                Status = "Pending"
            };

            await _paymentRepository.CreateAsync(payment);

            // Create PayPal order
            var returnUrl = $"https://qrbusinessexchange.com/payment/paypal/success?paymentId={payment.PaymentId}";
            var cancelUrl = $"https://qrbusinessexchange.com/payment/paypal/cancel?paymentId={payment.PaymentId}";

            var (orderId, approvalUrl) = await _payPalService.CreatePaymentAsync(
                amount,
                currency,
                returnUrl,
                cancelUrl,
                $"{subscriptionType} Subscription");

            // Update payment with transaction ID
            payment.TransactionId = orderId;
            await _paymentRepository.UpdateAsync(payment);

            return (orderId, approvalUrl);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Error creating PayPal payment", ex);
        }
    }

    /// <inheritdoc/>
    public async Task<bool> CapturePayPalPaymentAsync(string orderId)
    {
        if (string.IsNullOrWhiteSpace(orderId))
        {
            throw new ArgumentException("Order ID cannot be null or empty", nameof(orderId));
        }

        try
        {
            // Capture payment with PayPal
            var success = await _payPalService.CapturePaymentAsync(orderId);

            if (success)
            {
                // Update payment status in database
                var payment = await _paymentRepository.GetByTransactionIdAsync(orderId);
                if (payment != null)
                {
                    payment.Status = "Completed";
                    await _paymentRepository.UpdateAsync(payment);
                }
            }

            return success;
        }
        catch (Exception ex)
        {
            // Update payment status to Failed
            var payment = await _paymentRepository.GetByTransactionIdAsync(orderId);
            if (payment != null)
            {
                payment.Status = "Failed";
                payment.RejectionReason = ex.Message;
                await _paymentRepository.UpdateAsync(payment);
            }

            throw new InvalidOperationException("Error capturing PayPal payment", ex);
        }
    }

    /// <inheritdoc/>
    public async Task<string> CreateStripePaymentAsync(
        Guid userId, 
        decimal amount, 
        string currency, 
        string subscriptionType)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User ID cannot be empty", nameof(userId));
        }

        if (amount <= 0)
        {
            throw new ArgumentException("Amount must be greater than zero", nameof(amount));
        }

        if (string.IsNullOrWhiteSpace(currency))
        {
            throw new ArgumentException("Currency cannot be null or empty", nameof(currency));
        }

        try
        {
            // Create payment record in database
            var payment = new Payment
            {
                UserId = userId,
                Amount = amount,
                Currency = currency.ToUpper(),
                PaymentGateway = "Stripe",
                Status = "Pending"
            };

            await _paymentRepository.CreateAsync(payment);

            // Convert amount to cents for Stripe
            var amountInCents = (long)(amount * 100);

            // Create Stripe payment intent
            var clientSecret = await _stripeService.CreatePaymentIntentAsync(
                amountInCents,
                currency,
                userId,
                subscriptionType);

            // Extract payment intent ID from client secret (format: pi_xxx_secret_yyy)
            var parts = clientSecret.Split('_');
            if (parts.Length < 2 || !clientSecret.StartsWith("pi_"))
            {
                throw new InvalidOperationException("Invalid Stripe client secret format");
            }
            
            var paymentIntentId = $"{parts[0]}_{parts[1]}";
            
            // Update payment with transaction ID
            payment.TransactionId = paymentIntentId;
            await _paymentRepository.UpdateAsync(payment);

            return clientSecret;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Error creating Stripe payment", ex);
        }
    }

    /// <inheritdoc/>
    public async Task<bool> ConfirmStripePaymentAsync(string paymentIntentId)
    {
        if (string.IsNullOrWhiteSpace(paymentIntentId))
        {
            throw new ArgumentException("Payment intent ID cannot be null or empty", nameof(paymentIntentId));
        }

        try
        {
            // Confirm payment with Stripe
            var success = await _stripeService.ConfirmPaymentIntentAsync(paymentIntentId);

            if (success)
            {
                // Update payment status in database
                var payment = await _paymentRepository.GetByTransactionIdAsync(paymentIntentId);
                if (payment != null)
                {
                    payment.Status = "Completed";
                    await _paymentRepository.UpdateAsync(payment);
                }
            }

            return success;
        }
        catch (Exception ex)
        {
            // Update payment status to Failed
            var payment = await _paymentRepository.GetByTransactionIdAsync(paymentIntentId);
            if (payment != null)
            {
                payment.Status = "Failed";
                payment.RejectionReason = ex.Message;
                await _paymentRepository.UpdateAsync(payment);
            }

            throw new InvalidOperationException("Error confirming Stripe payment", ex);
        }
    }

    /// <inheritdoc/>
    public async Task<(string PaymentId, string Status)> CreateCloverPaymentAsync(
        Guid userId, 
        decimal amount, 
        string currency, 
        string subscriptionType)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User ID cannot be empty", nameof(userId));
        }

        if (amount <= 0)
        {
            throw new ArgumentException("Amount must be greater than zero", nameof(amount));
        }

        if (string.IsNullOrWhiteSpace(currency))
        {
            throw new ArgumentException("Currency cannot be null or empty", nameof(currency));
        }

        try
        {
            // Create payment record in database
            var payment = new Payment
            {
                UserId = userId,
                Amount = amount,
                Currency = currency.ToUpper(),
                PaymentGateway = "Clover",
                Status = "Pending"
            };

            await _paymentRepository.CreateAsync(payment);

            // Convert amount to cents for Clover
            var amountInCents = (long)(amount * 100);

            // NOTE: The source token should be obtained from Clover.js on the client side.
            // This placeholder will cause the API call to fail. In production, the controller
            // should accept and pass the actual source token from the client request.
            // TODO: Update IPaymentService interface to include source parameter for Clover payments
            var source = "clv_token_placeholder";

            // Create Clover charge
            var (paymentId, status) = await _cloverService.CreateChargeAsync(
                amountInCents,
                currency,
                source,
                userId,
                subscriptionType);

            // Update payment with transaction ID and status
            payment.TransactionId = paymentId;
            payment.Status = status.Equals("succeeded", StringComparison.OrdinalIgnoreCase) 
                ? "Completed" 
                : "Pending";
            
            await _paymentRepository.UpdateAsync(payment);

            return (paymentId, status);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Error creating Clover payment", ex);
        }
    }

    /// <inheritdoc/>
    public async Task<bool> VerifyPaymentLocationAsync(
        Guid paymentId, 
        decimal verificationLatitude, 
        decimal verificationLongitude)
    {
        if (paymentId == Guid.Empty)
        {
            throw new ArgumentException("Payment ID cannot be empty", nameof(paymentId));
        }

        try
        {
            var payment = await _paymentRepository.GetByIdAsync(paymentId);
            
            if (payment == null)
            {
                throw new InvalidOperationException($"Payment {paymentId} not found");
            }

            // Check if payment location coordinates are set
            if (!payment.PaymentLatitude.HasValue || !payment.PaymentLongitude.HasValue)
            {
                payment.Status = "Rejected";
                payment.RejectionReason = "Payment location coordinates not recorded";
                payment.IsVerified = false;
                await _paymentRepository.UpdateAsync(payment);
                return false;
            }

            // Update verification coordinates
            payment.VerificationLatitude = verificationLatitude;
            payment.VerificationLongitude = verificationLongitude;

            // Calculate distance between payment and verification locations
            var distance = _geolocationService.CalculateDistance(
                payment.PaymentLatitude.Value,
                payment.PaymentLongitude.Value,
                verificationLatitude,
                verificationLongitude);

            payment.DistanceVerification = distance;

            // Verify if distance is within acceptable range (configurable)
            var isVerified = _geolocationService.VerifyPaymentLocation(
                payment.PaymentLatitude.Value,
                payment.PaymentLongitude.Value,
                verificationLatitude,
                verificationLongitude,
                _paymentSettings.MaxVerificationDistanceYards);

            payment.IsVerified = isVerified;

            if (!isVerified)
            {
                payment.Status = "Rejected";
                payment.RejectionReason = $"Payment verification location is {distance:F2} yards away from payment initiation location. Maximum allowed distance is {_paymentSettings.MaxVerificationDistanceYards} yards.";
            }

            await _paymentRepository.UpdateAsync(payment);

            return isVerified;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Error verifying payment location", ex);
        }
    }
}
