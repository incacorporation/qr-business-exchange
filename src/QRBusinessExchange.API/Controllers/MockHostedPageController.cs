using System.Globalization;
using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using QRBusinessExchange.API.Services;

namespace QRBusinessExchange.API.Controllers;

/// <summary>
/// Development only: a stand-in for Fiserv's CardPointe Hosted Payment Page, served when the mock
/// gateway is on and no real hosted page is configured. Like the real page it takes ?total= and
/// ?invoice=, lets the amount be changed, and then "sends the webhook". Answers 404 otherwise.
/// </summary>
[ApiController]
[Route(MockHostedPayments.PagePath)]
[AllowAnonymous]
[ApiExplorerSettings(IgnoreApi = true)]
public class MockHostedPageController : ControllerBase
{
    private readonly CardPointeOptions _options;
    private readonly IHostEnvironment _environment;
    private readonly QrPaymentService _payments;

    public MockHostedPageController(IOptions<CardPointeOptions> options, IHostEnvironment environment, QrPaymentService payments)
    {
        _options = options.Value;
        _environment = environment;
        _payments = payments;
    }

    private bool Enabled => _options.UseMockApi && !_environment.IsProduction() && string.IsNullOrWhiteSpace(_options.HostedPageUrl);

    [HttpGet]
    public IActionResult Show([FromQuery] string? total, [FromQuery] string? invoice)
    {
        if (!Enabled)
        {
            return NotFound();
        }

        return Page($"""
            <h1>Test hosted payment page</h1>
            <p class="note">Stand-in for Fiserv's CardPointe Hosted Payment Page (mock gateway). No card is charged.</p>
            <form method="post">
              <label>Invoice # <input name="invoice" value="{E(invoice)}" readonly></label>
              <label>Total amount due <input name="total" value="{E(total)}" inputmode="decimal"></label>
              <label>Name on card <input name="name" value="Test Customer"></label>
              <label>Card number <input value="4111 1111 1111 1111" readonly></label>
              <button type="submit">Pay</button>
            </form>
            """);
    }

    [HttpPost]
    [Consumes("application/x-www-form-urlencoded")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Pay([FromForm] string? total, [FromForm] string? invoice, [FromForm] string? name)
    {
        if (!Enabled)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(invoice) ||
            !decimal.TryParse(total, NumberStyles.Number, CultureInfo.InvariantCulture, out var dollars) || dollars <= 0)
        {
            return Page("<h1>Check the amount</h1><p>Enter a total like 12.34.</p>");
        }

        var cents = (long)Math.Round(dollars * 100m, MidpointRounding.AwayFromZero);
        var transaction = MockHostedPayments.Record(invoice.Trim(), cents, name?.Trim());

        // What the real hosted page's webhook makes qrbound do.
        await _payments.ConfirmHostedPaymentAsync(invoice);

        return Page($"""
            <h1>Approved</h1>
            <p>{E((cents / 100m).ToString("0.00", CultureInfo.InvariantCulture))} paid for invoice {E(invoice)}.</p>
            <p>Reference {E(transaction.RetRef)}</p>
            <p class="note">You can close this tab and go back to the bill.</p>
            """);
    }

    private static string E(string? text) => WebUtility.HtmlEncode(text ?? string.Empty);

    private ContentResult Page(string body) => Content($$"""
        <!DOCTYPE html>
        <html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
        <title>Test hosted payment page</title>
        <style>
          body { font-family: system-ui, sans-serif; max-width: 26rem; margin: 2rem auto; padding: 0 1rem; color: #1d2939; }
          label { display: block; margin: .75rem 0; font-size: .875rem; }
          input { display: block; width: 100%; padding: .5rem; margin-top: .25rem; box-sizing: border-box; }
          button { width: 100%; padding: .75rem; background: #2e90fa; color: #fff; border: 0; border-radius: .5rem; font-weight: 600; }
          .note { color: #b54708; font-size: .8125rem; }
        </style></head><body>{{body}}</body></html>
        """, "text/html");
}
