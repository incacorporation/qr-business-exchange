using System.Net;
using System.Net.Http.Json;
using Qrbound.Payments.Models;

namespace Qrbound.Payments;

/// <summary>
/// Calls the server's card payment endpoints from Blazor.
/// Register it with <c>AddQrboundPayments</c>.
/// </summary>
public sealed class QrboundPaymentsClient(HttpClient http)
{
    /// <summary>Route of the server's payment controller, relative to the API base address.</summary>
    public const string RoutePrefix = "api/cardpointe";

    /// <summary>Gets the public settings for the card form.</summary>
    public async Task<PaymentConfig> GetConfigAsync(CancellationToken cancellationToken = default)
    {
        return await http.GetFromJsonAsync<PaymentConfig>($"{RoutePrefix}/config", cancellationToken)
            ?? throw new InvalidOperationException("The server returned no payment configuration.");
    }

    /// <summary>
    /// Charges a tokenized card. A decline comes back as a result with IsSuccess false;
    /// so does a request the server rejects (bad amount, missing token), with its reason.
    /// </summary>
    public async Task<CardPaymentResult> ChargeAsync(CardPaymentRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var response = await http.PostAsJsonAsync($"{RoutePrefix}/process", request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<CardPaymentResult>(cancellationToken)
                ?? throw new InvalidOperationException("The server returned an empty payment result.");
        }

        var reason = response.StatusCode switch
        {
            HttpStatusCode.BadRequest => await ReadErrorAsync(response, cancellationToken) ?? "The payment details were not accepted.",
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "Sign in to make a payment.",
            _ => "The payment could not be processed. Please try again."
        };

        return new CardPaymentResult
        {
            IsSuccess = false,
            StatusMessage = "Error",
            ErrorMessage = reason,
            AmountCents = request.AmountCents
        };
    }

    /// <summary>Route of the server's public QR payment endpoints.</summary>
    public const string PayRoutePrefix = "api/pay";

    /// <summary>Gets a QR payment request by its link ID, or null when there's no such link.</summary>
    public async Task<QrPaymentInfo?> GetQrPaymentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync($"{PayRoutePrefix}/{id}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<QrPaymentInfo>(cancellationToken);
    }

    /// <summary>
    /// Pays a QR payment request with a tokenized card; the server charges the request's own amount.
    /// Declines, and links that can no longer be paid, come back as a result with IsSuccess false.
    /// </summary>
    public async Task<CardPaymentResult> PayQrPaymentAsync(Guid id, QrPaymentPayRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var response = await http.PostAsJsonAsync($"{PayRoutePrefix}/{id}", request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<CardPaymentResult>(cancellationToken)
                ?? throw new InvalidOperationException("The server returned an empty payment result.");
        }

        var reason = response.StatusCode switch
        {
            HttpStatusCode.BadRequest or HttpStatusCode.Conflict or HttpStatusCode.NotFound =>
                await ReadErrorAsync(response, cancellationToken) ?? "This payment can't be made.",
            _ => "The payment could not be processed. Please try again."
        };

        return new CardPaymentResult { IsSuccess = false, StatusMessage = "Error", ErrorMessage = reason };
    }

    /// <summary>
    /// Locks the customer's tip into a quote for the bill; pass its QuoteId to every way of paying.
    /// Throws <see cref="QrboundPaymentException"/> with the server's reason when the tip isn't accepted.
    /// </summary>
    public async Task<QrPaymentQuote> QuoteQrPaymentAsync(Guid id, long tipCents, CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsJsonAsync($"{PayRoutePrefix}/{id}/quote", new QrPaymentQuoteRequest { TipCents = tipCents }, cancellationToken);
        return await ReadOrThrowAsync<QrPaymentQuote>(response, "The total couldn't be worked out. Please try again.", cancellationToken);
    }

    /// <summary>
    /// The address of Fiserv's CardPointe Hosted Payment Page for a quote. Send the customer there
    /// (a link they tap); afterwards <see cref="CheckHostedPaymentAsync"/> tells whether they paid.
    /// </summary>
    public async Task<HostedPageStart> StartHostedPaymentAsync(Guid id, Guid quoteId, CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsJsonAsync($"{PayRoutePrefix}/{id}/hosted", new HostedPageStartRequest { QuoteId = quoteId }, cancellationToken);
        return await ReadOrThrowAsync<HostedPageStart>(response, "The secure card page isn't available. Please try again.", cancellationToken);
    }

    /// <summary>Asks the server to check with CardPointe whether the bill was paid on the hosted page.</summary>
    public async Task<QrPaymentInfo> CheckHostedPaymentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsync($"{PayRoutePrefix}/{id}/hosted/check", null, cancellationToken);
        return await ReadOrThrowAsync<QrPaymentInfo>(response, "We couldn't check the payment. Please try again.", cancellationToken);
    }

    private static async Task<T> ReadOrThrowAsync<T>(HttpResponseMessage response, string fallback, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<T>(cancellationToken)
                ?? throw new QrboundPaymentException(fallback);
        }

        var reason = response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict or HttpStatusCode.NotFound
            ? await ReadErrorAsync(response, cancellationToken)
            : null;
        throw new QrboundPaymentException(reason ?? fallback);
    }

    private static async Task<string?> ReadErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var body = await response.Content.ReadFromJsonAsync<ErrorBody>(cancellationToken);
            return body?.Error ?? body?.Title;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private sealed record ErrorBody(string? Error, string? Title);
}

/// <summary>The server refused a payment step; Message is safe to show to the customer.</summary>
public sealed class QrboundPaymentException(string message) : Exception(message);
