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
/// The payer's side of a QR payment: anyone with the link can see it and pay it, without an account.
/// The amount always comes from the stored request.
/// </summary>
[ApiController]
[Route("api/pay")]
[AllowAnonymous]
public class PayController : ControllerBase
{
    private readonly QrPaymentService _payments;

    public PayController(QrPaymentService payments) => _payments = payments;

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(QrPaymentInfo), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id)
    {
        try
        {
            return Ok(await _payments.GetPublicAsync(id));
        }
        catch (QrPaymentException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
    }

    /// <summary>Pays the request with a tokenized card. A decline returns 200 with IsSuccess false.</summary>
    [HttpPost("{id:guid}")]
    [ProducesResponseType(typeof(CardPaymentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Pay(Guid id, [FromBody] QrPaymentPayRequest request)
    {
        try
        {
            return Ok(await _payments.PayAsync(id, request));
        }
        catch (QrPaymentException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
    }

    /// <summary>
    /// [Signed in] Step 1 of paying with a saved card: passkey options bound to this payment, card and amount.
    /// </summary>
    [HttpPost("{id:guid}/saved-card/init")]
    [Authorize]
    [ProducesResponseType(typeof(QRBusinessExchange.Core.DTOs.PasskeyAuthenticationOptionsDto), StatusCodes.Status200OK)]
    public Task<IActionResult> SavedCardInit(Guid id, [FromBody] QRBusinessExchange.Core.DTOs.SavedCardPayInitRequest request) =>
        RunSignedIn(userId => _payments.BeginSavedCardPaymentAsync(userId, id, request.SavedCardId));

    /// <summary>
    /// [Signed in] Step 2: pays with the saved card once the passkey assertion (fingerprint / face) checks out.
    /// A decline returns 200 with IsSuccess false.
    /// </summary>
    [HttpPost("{id:guid}/saved-card")]
    [Authorize]
    [ProducesResponseType(typeof(CardPaymentResult), StatusCodes.Status200OK)]
    public Task<IActionResult> PaySavedCard(Guid id, [FromBody] QRBusinessExchange.Core.DTOs.SavedCardPayRequest request) =>
        RunSignedIn(userId => _payments.PaySavedCardAsync(userId, id, request));

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
