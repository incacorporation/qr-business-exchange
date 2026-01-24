using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QRBusinessExchange.API.DTOs;
using QRBusinessExchange.Core.Entities;
using QRBusinessExchange.Core.Interfaces;

namespace QRBusinessExchange.API.Controllers;

/// <summary>
/// Controller for payment processing and verification
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PaymentController : ControllerBase
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly IGeolocationService _geolocationService;
    private readonly ILogger<PaymentController> _logger;

    public PaymentController(
        IPaymentRepository paymentRepository,
        IGeolocationService geolocationService,
        ILogger<PaymentController> logger)
    {
        _paymentRepository = paymentRepository;
        _geolocationService = geolocationService;
        _logger = logger;
    }

    /// <summary>
    /// Initiate a payment transaction
    /// </summary>
    /// <param name="request">Payment details</param>
    /// <returns>Created payment transaction</returns>
    [HttpPost]
    [ProducesResponseType(typeof(PaymentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> InitiatePayment([FromBody] PaymentRequest request)
    {
        var userId = GetCurrentUserId();

        var payment = new Payment
        {
            UserId = userId,
            Amount = request.Amount,
            Currency = request.Currency,
            PaymentGateway = request.PaymentGateway,
            Status = "Pending",
            PaymentLatitude = request.Latitude,
            PaymentLongitude = request.Longitude,
            IsVerified = false,
            CreatedAt = DateTime.UtcNow
        };

        var createdPayment = await _paymentRepository.CreateAsync(payment);

        return CreatedAtAction(nameof(GetPayment), new { id = createdPayment.PaymentId }, MapToPaymentDto(createdPayment));
    }

    /// <summary>
    /// Get payment by ID
    /// </summary>
    /// <param name="id">Payment ID</param>
    /// <returns>Payment details</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(PaymentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPayment(Guid id)
    {
        var payment = await _paymentRepository.GetByIdAsync(id);
        
        if (payment == null)
        {
            return NotFound(new { error = "Payment not found" });
        }

        var userId = GetCurrentUserId();
        if (payment.UserId != userId)
        {
            return Forbid();
        }

        return Ok(MapToPaymentDto(payment));
    }

    /// <summary>
    /// Verify payment location
    /// </summary>
    /// <param name="id">Payment ID</param>
    /// <param name="latitude">Verification latitude</param>
    /// <param name="longitude">Verification longitude</param>
    /// <returns>Verification result</returns>
    [HttpPost("{id}/verify-location")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> VerifyLocation(Guid id, [FromQuery] decimal latitude, [FromQuery] decimal longitude)
    {
        var payment = await _paymentRepository.GetByIdAsync(id);
        
        if (payment == null)
        {
            return NotFound(new { error = "Payment not found" });
        }

        var userId = GetCurrentUserId();
        if (payment.UserId != userId)
        {
            return Forbid();
        }

        if (!payment.PaymentLatitude.HasValue || !payment.PaymentLongitude.HasValue)
        {
            return BadRequest(new { error = "Payment location not recorded" });
        }

        // Calculate distance
        var distance = _geolocationService.CalculateDistance(
            payment.PaymentLatitude.Value,
            payment.PaymentLongitude.Value,
            latitude,
            longitude
        );

        payment.VerificationLatitude = latitude;
        payment.VerificationLongitude = longitude;
        payment.DistanceVerification = distance;

        // Verify if within acceptable range (100 yards)
        const decimal maxDistanceYards = 100;
        if (distance <= maxDistanceYards)
        {
            payment.IsVerified = true;
            payment.Status = "Completed";
        }
        else
        {
            payment.IsVerified = false;
            payment.Status = "Rejected";
            payment.RejectionReason = $"Location verification failed. Distance: {distance} yards exceeds maximum allowed: {maxDistanceYards} yards";
        }

        var updatedPayment = await _paymentRepository.UpdateAsync(payment);

        return Ok(new
        {
            isVerified = updatedPayment.IsVerified,
            distanceYards = distance,
            status = updatedPayment.Status,
            message = updatedPayment.IsVerified 
                ? "Payment location verified successfully" 
                : updatedPayment.RejectionReason
        });
    }

    /// <summary>
    /// Process PayPal payment
    /// </summary>
    /// <param name="request">Payment details</param>
    /// <returns>Payment result</returns>
    [HttpPost("paypal")]
    [ProducesResponseType(typeof(PaymentDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ProcessPayPalPayment([FromBody] PaymentRequest request)
    {
        request.PaymentGateway = "PayPal";
        return await InitiatePayment(request);
    }

    /// <summary>
    /// Process Stripe payment
    /// </summary>
    /// <param name="request">Payment details</param>
    /// <returns>Payment result</returns>
    [HttpPost("stripe")]
    [ProducesResponseType(typeof(PaymentDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ProcessStripePayment([FromBody] PaymentRequest request)
    {
        request.PaymentGateway = "Stripe";
        return await InitiatePayment(request);
    }

    /// <summary>
    /// Process Clover payment
    /// </summary>
    /// <param name="request">Payment details</param>
    /// <returns>Payment result</returns>
    [HttpPost("clover")]
    [ProducesResponseType(typeof(PaymentDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ProcessCloverPayment([FromBody] PaymentRequest request)
    {
        request.PaymentGateway = "Clover";
        return await InitiatePayment(request);
    }

    /// <summary>
    /// Get payment history for current user
    /// </summary>
    /// <returns>List of payments</returns>
    [HttpGet("history")]
    [ProducesResponseType(typeof(IEnumerable<PaymentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHistory()
    {
        var userId = GetCurrentUserId();
        var payments = await _paymentRepository.GetByUserIdAsync(userId);
        
        return Ok(payments.Select(MapToPaymentDto));
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = HttpContext.Items["UserId"];
        if (userIdClaim != null && userIdClaim is Guid userId)
        {
            return userId;
        }

        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier) 
                    ?? User.FindFirst("sub");
        
        if (claim != null && Guid.TryParse(claim.Value, out var parsedUserId))
        {
            return parsedUserId;
        }

        throw new UnauthorizedAccessException("User ID not found in token");
    }

    private static PaymentDto MapToPaymentDto(Payment payment)
    {
        return new PaymentDto
        {
            PaymentId = payment.PaymentId,
            Amount = payment.Amount,
            Currency = payment.Currency,
            PaymentGateway = payment.PaymentGateway,
            TransactionId = payment.TransactionId,
            Status = payment.Status,
            PaymentLatitude = payment.PaymentLatitude,
            PaymentLongitude = payment.PaymentLongitude,
            VerificationLatitude = payment.VerificationLatitude,
            VerificationLongitude = payment.VerificationLongitude,
            DistanceVerification = payment.DistanceVerification,
            IsVerified = payment.IsVerified,
            CreatedAt = payment.CreatedAt
        };
    }
}
