using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Qrbound.Payments.Models;
using QRBusinessExchange.API.Extensions;
using QRBusinessExchange.API.Services;

namespace QRBusinessExchange.API.Controllers;

/// <summary>
/// Staff of a business create QR payment requests, watch them get paid, and cancel or resolve them.
/// Any member of the business may use these.
/// </summary>
[ApiController]
[Route("api/businesses/{businessId:guid}/qr-payments")]
[Authorize]
public class QrPaymentsController : ControllerBase
{
    private readonly QrPaymentService _payments;

    public QrPaymentsController(QrPaymentService payments) => _payments = payments;

    /// <summary>Creates a payment request; show its /pay/{id} link as a QR code.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(QrPaymentDetails), StatusCodes.Status200OK)]
    public Task<IActionResult> Create(Guid businessId, [FromBody] CreateQrPaymentRequest request) =>
        Run(userId => _payments.CreateAsync(userId, businessId, request));

    /// <summary>The business's most recent payment requests, newest first.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<QrPaymentDetails>), StatusCodes.Status200OK)]
    public Task<IActionResult> List(Guid businessId, [FromQuery] int take = 50, [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null) =>
        Run(userId => _payments.ListAsync(userId, businessId, take, from, to));

    /// <summary>One payment request; poll this to see it get paid.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(QrPaymentDetails), StatusCodes.Status200OK)]
    public Task<IActionResult> Get(Guid businessId, Guid id) =>
        Run(userId => _payments.GetAsync(userId, businessId, id));

    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(typeof(QrPaymentDetails), StatusCodes.Status200OK)]
    public Task<IActionResult> Cancel(Guid businessId, Guid id) =>
        Run(userId => _payments.CancelAsync(userId, businessId, id));

    /// <summary>Settles a request in NeedsReview after checking CardPointe.</summary>
    [HttpPost("{id:guid}/resolve")]
    [ProducesResponseType(typeof(QrPaymentDetails), StatusCodes.Status200OK)]
    public Task<IActionResult> Resolve(Guid businessId, Guid id, [FromBody] ResolveQrPaymentRequest request) =>
        Run(userId => _payments.ResolveAsync(userId, businessId, id, request));

    /// <summary>Asks CardPointe about this bill's order and records any approved payment (hosted page, or after a timeout).</summary>
    [HttpPost("{id:guid}/check-gateway")]
    [ProducesResponseType(typeof(QrPaymentDetails), StatusCodes.Status200OK)]
    public Task<IActionResult> CheckGateway(Guid businessId, Guid id) =>
        Run(userId => _payments.CheckWithGatewayAsync(userId, businessId, id));

    private async Task<IActionResult> Run<T>(Func<Guid, Task<T>> action)
    {
        var userId = User.GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        try
        {
            return Ok(await action(userId.Value));
        }
        catch (QrPaymentException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
    }
}

/// <summary>
/// The payer's side of a QR payment: anyone with the link can see it and pay it by card, without an
/// account. Paying with a saved card needs a signed-in customer. The amount always comes from the
/// stored request.
/// </summary>
[ApiController]
[Route("api/pay")]
public class PayController : ControllerBase
{
    private readonly QrPaymentService _payments;

    private readonly CardPointeOptions _options;
    private readonly ILogger<PayController> _logger;

    public PayController(QrPaymentService payments, Microsoft.Extensions.Options.IOptions<CardPointeOptions> options, ILogger<PayController> logger)
    {
        _payments = payments;
        _options = options.Value;
        _logger = logger;
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(QrPaymentInfo), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> Get(Guid id) => RunAnonymous(() => _payments.GetPublicAsync(id));

    /// <summary>Locks the customer's tip into a quote; every way of paying names it.</summary>
    [HttpPost("{id:guid}/quote")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(QrPaymentQuote), StatusCodes.Status200OK)]
    public Task<IActionResult> Quote(Guid id, [FromBody] QrPaymentQuoteRequest request) =>
        RunAnonymous(() => _payments.QuoteAsync(id, request));

    /// <summary>Pays the request with a tokenized card. A decline returns 200 with IsSuccess false.</summary>
    [HttpPost("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(CardPaymentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<IActionResult> Pay(Guid id, [FromBody] QrPaymentPayRequest request) =>
        RunAnonymous(() => _payments.PayAsync(id, request));

    /// <summary>The address of Fiserv's hosted payment page for the quote's total; the customer pays there.</summary>
    [HttpPost("{id:guid}/hosted")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(HostedPageStart), StatusCodes.Status200OK)]
    public Task<IActionResult> StartHosted(Guid id, [FromBody] HostedPageStartRequest request) =>
        RunAnonymous(() => _payments.StartHostedPaymentAsync(id, request, new Uri($"{Request.Scheme}://{Request.Host}{Request.PathBase}/")));

    /// <summary>Asks CardPointe whether the customer has paid on the hosted page; returns the bill.</summary>
    [HttpPost("{id:guid}/hosted/check")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(QrPaymentInfo), StatusCodes.Status200OK)]
    public Task<IActionResult> CheckHosted(Guid id) => RunAnonymous(() => _payments.CheckHostedPaymentAsync(id));

    /// <summary>
    /// Fiserv's hosted payment page posts here after a payment (set this URL, with ?key=, in the HPP
    /// Admin's Connect tab). Only the invoice number is read from it: the payment itself is looked up
    /// with CardPointe before anything is recorded.
    /// </summary>
    [HttpPost("hosted/webhook")]
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> HostedWebhook([FromQuery] string? key)
    {
        if (!string.IsNullOrEmpty(_options.HostedPageWebhookKey))
        {
            var expected = System.Text.Encoding.UTF8.GetBytes(_options.HostedPageWebhookKey);
            var given = System.Text.Encoding.UTF8.GetBytes(key ?? string.Empty);
            if (!System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(expected, given))
            {
                _logger.LogWarning("Hosted payment page webhook with a wrong key from {Ip}", HttpContext.Connection.RemoteIpAddress);
                return Unauthorized();
            }
        }
        else
        {
            _logger.LogWarning("CardPointe:HostedPageWebhookKey isn't set; accepting the hosted page webhook without a key");
        }

        string body;
        using (var reader = new StreamReader(Request.Body))
        {
            body = await reader.ReadToEndAsync();
        }

        var invoice = HostedPageWebhookParser.FindInvoice(body, Request.ContentType);
        if (invoice == null)
        {
            _logger.LogWarning("Hosted payment page webhook without an invoice number ({Length} bytes)", body.Length);
            return Ok();
        }

        // Answer 200 either way, so Fiserv doesn't keep retrying a webhook for a bill we don't have.
        await _payments.ConfirmHostedPaymentAsync(invoice);
        return Ok();
    }

    /// <summary>
    /// [Signed in] Step 1 of paying with a saved card: passkey options bound to this payment, card and quote.
    /// </summary>
    [HttpPost("{id:guid}/saved-card/init")]
    [Authorize]
    [ProducesResponseType(typeof(QRBusinessExchange.Core.DTOs.PasskeyAuthenticationOptionsDto), StatusCodes.Status200OK)]
    public Task<IActionResult> SavedCardInit(Guid id, [FromBody] QRBusinessExchange.Core.DTOs.SavedCardPayInitRequest request) =>
        RunSignedIn(userId => _payments.BeginSavedCardPaymentAsync(userId, id, request.SavedCardId, request.QuoteId));

    /// <summary>
    /// [Signed in] Step 2: pays with the saved card once the passkey assertion (fingerprint / face) checks out.
    /// A decline returns 200 with IsSuccess false.
    /// </summary>
    [HttpPost("{id:guid}/saved-card")]
    [Authorize]
    [ProducesResponseType(typeof(CardPaymentResult), StatusCodes.Status200OK)]
    public Task<IActionResult> PaySavedCard(Guid id, [FromBody] QRBusinessExchange.Core.DTOs.SavedCardPayRequest request) =>
        RunSignedIn(userId => _payments.PaySavedCardAsync(userId, id, request));

    private async Task<IActionResult> RunAnonymous<T>(Func<Task<T>> action)
    {
        try
        {
            return Ok(await action());
        }
        catch (QrPaymentException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
    }

    private async Task<IActionResult> RunSignedIn<T>(Func<Guid, Task<T>> action)
    {
        var userId = User.GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        try
        {
            return Ok(await action(userId.Value));
        }
        catch (QrPaymentException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
    }
}
