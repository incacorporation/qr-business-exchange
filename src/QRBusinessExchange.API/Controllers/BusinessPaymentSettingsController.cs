using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QRBusinessExchange.API.Extensions;
using QRBusinessExchange.API.Services;
using QRBusinessExchange.Core.DTOs;

namespace QRBusinessExchange.API.Controllers;

/// <summary>
/// A business's own payment settings and payments report. Members can read the settings; only
/// Owners and Admins can change them or see the report.
/// </summary>
[ApiController]
[Route("api/businesses/{businessId:guid}")]
[Authorize]
public class BusinessPaymentSettingsController : ControllerBase
{
    private readonly BusinessPaymentSettingsService _settings;

    public BusinessPaymentSettingsController(BusinessPaymentSettingsService settings) => _settings = settings;

    [HttpGet("payment-settings")]
    [ProducesResponseType(typeof(BusinessPaymentSettingsDto), StatusCodes.Status200OK)]
    public Task<IActionResult> Get(Guid businessId) => Run(userId => _settings.GetAsync(userId, businessId));

    [HttpPut("payment-settings")]
    [ProducesResponseType(typeof(BusinessPaymentSettingsDto), StatusCodes.Status200OK)]
    public Task<IActionResult> Update(Guid businessId, [FromBody] UpdateBusinessPaymentSettingsRequest request) =>
        Run(userId => _settings.UpdateAsync(userId, businessId, request));

    /// <summary>Totals for QR payments created between two dates (UTC, inclusive). Defaults to the last 30 days.</summary>
    [HttpGet("qr-payments/report")]
    [ProducesResponseType(typeof(QrPaymentReportDto), StatusCodes.Status200OK)]
    public Task<IActionResult> Report(Guid businessId, [FromQuery] DateTime? from, [FromQuery] DateTime? to) =>
        Run(userId => _settings.ReportAsync(userId, businessId, from, to));

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
