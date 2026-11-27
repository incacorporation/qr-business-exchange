using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Qrbound.Payments.Models;
using QRBusinessExchange.API.Services;

namespace QRBusinessExchange.API.Controllers;

/// <summary>
/// Card payments through CardPointe. The browser tokenizes the card with CardPointe's hosted
/// iFrame and sends only the token here; card numbers never reach this API.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class CardPointeController : ControllerBase
{
    private readonly ICardPointeService _cardPointeService;
    private readonly CardPointeOptions _options;
    private readonly ILogger<CardPointeController> _logger;

    public CardPointeController(
        ICardPointeService cardPointeService,
        IOptions<CardPointeOptions> options,
        ILogger<CardPointeController> logger)
    {
        _cardPointeService = cardPointeService;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Public settings for the card form: merchant ID, tokenizer URL, currency and amount limits.
    /// Never includes the API username or password.
    /// </summary>
    [HttpGet("config")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PaymentConfig), StatusCodes.Status200OK)]
    public ActionResult<PaymentConfig> GetConfig() => Ok(_cardPointeService.GetPublicConfig());

    /// <summary>
    /// Charges a tokenized card (authorize and capture). A decline returns 200 with IsSuccess false.
    /// </summary>
    [HttpPost("process")]
    [Authorize]
    [ProducesResponseType(typeof(CardPaymentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CardPaymentResult>> ProcessPayment(
        [FromBody] CardPaymentRequest request, CancellationToken cancellationToken)
    {
        // [ApiController] has already rejected requests that fail the model's validation attributes.
        if (!string.Equals(request.Currency, _options.Currency, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { error = $"Payments are taken in {_options.Currency} only." });
        }

        if (request.AmountCents < _options.MinAmountCents || request.AmountCents > _options.MaxAmountCents)
        {
            return BadRequest(new
            {
                error = $"Amount must be between {_options.MinAmountCents / 100m:0.00} and {_options.MaxAmountCents / 100m:0.00} {_options.Currency}."
            });
        }

        var response = await _cardPointeService.ChargeAsync(new CardPointeChargeRequest
        {
            Token = request.PaymentToken,
            Expiry = request.Expiry,
            AmountCents = request.AmountCents,
            Currency = _options.Currency,
            OrderId = request.InvoiceNumber,
            CardholderName = request.CardholderName,
            Email = request.Email
        }, cancellationToken);

        if (!response.IsSuccess)
        {
            _logger.LogWarning("Card payment for invoice {Invoice} not approved: {Status}", request.InvoiceNumber, response.StatusMessage);
        }

        return Ok(new CardPaymentResult
        {
            IsSuccess = response.IsSuccess,
            TransactionId = response.TransactionId,
            AuthorizationCode = response.AuthorizationCode,
            StatusMessage = response.StatusMessage,
            ErrorMessage = response.ErrorMessage,
            AmountCents = request.AmountCents,
            TransactionDateTime = DateTime.UtcNow
        });
    }
}
