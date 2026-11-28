using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qrbound.Payments.Models;

namespace Qrbound.POS.Services;

/// <summary>
/// The staff side of qrbound QR payments, called from the POS server: creates a payment request for
/// an order, reads it back and cancels it. Signs in as the service account in
/// <see cref="QrboundConnectionOptions"/> and keeps its token until shortly before it expires.
/// </summary>
public sealed class QrboundBusinessClient(HttpClient http, IOptions<QrboundPosOptions> options, ILogger<QrboundBusinessClient> logger)
{
    private static readonly SemaphoreSlim SignInLock = new(1, 1);
    private static string? cachedToken;
    private static DateTime cachedUntil;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(options.Value.Qrbound.ServiceAccountEmail) &&
        !string.IsNullOrWhiteSpace(options.Value.Qrbound.ServiceAccountPassword);

    public Task<QrPaymentDetails> CreatePaymentAsync(Guid businessId, long amountCents, string description, CancellationToken ct = default) =>
        SendAsync<QrPaymentDetails>(HttpMethod.Post, $"api/businesses/{businessId}/qr-payments", new CreateQrPaymentRequest
        {
            AmountCents = amountCents,
            Description = description.Length > 250 ? description[..250] : description,
            ExpiresInMinutes = Math.Clamp(options.Value.Qrbound.PaymentExpiresInMinutes, 5, 1440)
        }, ct);

    public Task<QrPaymentDetails> GetPaymentAsync(Guid businessId, Guid paymentId, CancellationToken ct = default) =>
        SendAsync<QrPaymentDetails>(HttpMethod.Get, $"api/businesses/{businessId}/qr-payments/{paymentId}", null, ct);

    public Task<QrPaymentDetails> CancelPaymentAsync(Guid businessId, Guid paymentId, CancellationToken ct = default) =>
        SendAsync<QrPaymentDetails>(HttpMethod.Post, $"api/businesses/{businessId}/qr-payments/{paymentId}/cancel", null, ct);

    /// <summary>Asks qrbound to look the bill up with CardPointe (hosted page payments, or after a timeout).</summary>
    public Task<QrPaymentDetails> CheckWithGatewayAsync(Guid businessId, Guid paymentId, CancellationToken ct = default) =>
        SendAsync<QrPaymentDetails>(HttpMethod.Post, $"api/businesses/{businessId}/qr-payments/{paymentId}/check-gateway", null, ct);

    private async Task<T> SendAsync<T>(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            var token = await GetTokenAsync(forceNew: attempt > 0, ct);
            using var request = new HttpRequestMessage(method, path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (body is not null) request.Content = JsonContent.Create(body, body.GetType());

            using var response = await http.SendAsync(request, ct);
            if (response.StatusCode == HttpStatusCode.Unauthorized && attempt == 0)
            {
                continue; // token expired early or was revoked: sign in again once
            }

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<T>(ct) ?? throw new PosException("qrbound returned an empty answer.");
            }

            var reason = await ReadErrorAsync(response, ct);
            logger.LogWarning("qrbound {Method} {Path} answered {Status}: {Reason}", method, path, (int)response.StatusCode, reason);
            throw new PosException(response.StatusCode switch
            {
                HttpStatusCode.Forbidden => "The POS's qrbound account can't take payments for this business. Check its role in the business.",
                HttpStatusCode.Unauthorized => "The POS couldn't sign in to qrbound. Check the service account.",
                _ => reason ?? "qrbound couldn't take the payment right now. Please try again."
            });
        }
    }

    private async Task<string> GetTokenAsync(bool forceNew, CancellationToken ct)
    {
        if (!forceNew && cachedToken is not null && DateTime.UtcNow < cachedUntil) return cachedToken;

        await SignInLock.WaitAsync(ct);
        try
        {
            if (!forceNew && cachedToken is not null && DateTime.UtcNow < cachedUntil) return cachedToken;

            var o = options.Value.Qrbound;
            if (!IsConfigured)
            {
                throw new PosException("Card payments aren't set up: QrboundPos:Qrbound:ServiceAccountEmail and ServiceAccountPassword are missing.");
            }

            using var response = await http.PostAsJsonAsync("api/auth/login", new { email = o.ServiceAccountEmail, password = o.ServiceAccountPassword }, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("qrbound sign-in for the POS service account failed with {Status}", (int)response.StatusCode);
                throw new PosException("The POS couldn't sign in to qrbound. Check the service account.");
            }

            var auth = await response.Content.ReadFromJsonAsync<LoginAnswer>(ct);
            if (string.IsNullOrEmpty(auth?.AccessToken)) throw new PosException("qrbound sign-in returned no token.");

            cachedToken = auth.AccessToken;
            var expires = auth.ExpiresAt > DateTime.UtcNow ? auth.ExpiresAt : DateTime.UtcNow.AddMinutes(30);
            cachedUntil = expires.AddMinutes(-2);
            return cachedToken;
        }
        finally
        {
            SignInLock.Release();
        }
    }

    private static async Task<string?> ReadErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var body = await response.Content.ReadFromJsonAsync<ErrorAnswer>(ct);
            return body?.Error ?? body?.Title;
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or NotSupportedException)
        {
            return null;
        }
    }

    private sealed record LoginAnswer(string AccessToken, DateTime ExpiresAt);
    private sealed record ErrorAnswer(string? Error, string? Title);
}
