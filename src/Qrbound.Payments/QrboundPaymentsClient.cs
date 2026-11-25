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
