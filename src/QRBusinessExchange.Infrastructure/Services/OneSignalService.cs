using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using QRBusinessExchange.Core.Interfaces;
using QRBusinessExchange.Infrastructure.Configuration;
using QRBusinessExchange.Infrastructure.Data;

namespace QRBusinessExchange.Infrastructure.Services;

/// <summary>
/// Service for sending push notifications via OneSignal API
/// </summary>
public class OneSignalService : INotificationService
{
    private readonly OneSignalSettings _oneSignalSettings;
    private readonly ApplicationDbContext _context;
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="OneSignalService"/> class
    /// </summary>
    /// <param name="oneSignalSettings">OneSignal configuration settings</param>
    /// <param name="context">The application database context</param>
    /// <param name="httpClient">HTTP client for API requests</param>
    public OneSignalService(IOptions<OneSignalSettings> oneSignalSettings, ApplicationDbContext context, HttpClient httpClient)
    {
        _oneSignalSettings = oneSignalSettings?.Value ?? throw new ArgumentNullException(nameof(oneSignalSettings));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

        ValidateSettings();
        ConfigureHttpClient();
    }

    /// <inheritdoc/>
    public async Task<bool> SendPushNotificationAsync(Guid userId, string title, string message, Dictionary<string, string>? data = null)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Title cannot be null or empty", nameof(title));
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Message cannot be null or empty", nameof(message));
        }

        try
        {
            // Get user to verify existence
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                throw new InvalidOperationException($"User with ID {userId} not found");
            }

            // Build notification payload
            var payload = new
            {
                app_id = _oneSignalSettings.AppId,
                include_external_user_ids = new[] { userId.ToString() },
                headings = new { en = title },
                contents = new { en = message },
                data = data ?? new Dictionary<string, string>()
            };

            var jsonPayload = JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_oneSignalSettings.ApiBaseUrl}/notifications", content);

            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            Console.Error.WriteLine($"OneSignal API error: {response.StatusCode} - {errorContent}");
            return false;
        }
        catch (HttpRequestException ex)
        {
            Console.Error.WriteLine($"HTTP error sending push notification to user {userId}: {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error sending push notification to user {userId}: {ex.Message}");
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> SendConnectionRequestAsync(Guid recipientUserId, Guid senderUserId, string senderName, string? message = null)
    {
        if (string.IsNullOrWhiteSpace(senderName))
        {
            throw new ArgumentException("Sender name cannot be null or empty", nameof(senderName));
        }

        try
        {
            // Verify both users exist
            var recipient = await _context.Users.FindAsync(recipientUserId);
            if (recipient == null)
            {
                throw new InvalidOperationException($"Recipient user with ID {recipientUserId} not found");
            }

            var sender = await _context.Users.FindAsync(senderUserId);
            if (sender == null)
            {
                throw new InvalidOperationException($"Sender user with ID {senderUserId} not found");
            }

            var title = "New Connection Request";
            var notificationMessage = !string.IsNullOrWhiteSpace(message)
                ? $"{senderName} sent you a connection request: {message}"
                : $"{senderName} sent you a connection request";

            var data = new Dictionary<string, string>
            {
                { "type", "connection_request" },
                { "senderId", senderUserId.ToString() },
                { "senderName", senderName },
                { "timestamp", DateTime.UtcNow.ToString("O") }
            };

            if (!string.IsNullOrWhiteSpace(message))
            {
                data.Add("message", message);
            }

            return await SendPushNotificationAsync(recipientUserId, title, notificationMessage, data);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error sending connection request notification: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Validates OneSignal settings configuration
    /// </summary>
    private void ValidateSettings()
    {
        if (string.IsNullOrWhiteSpace(_oneSignalSettings.AppId))
        {
            throw new InvalidOperationException("OneSignal App ID is not configured");
        }

        if (string.IsNullOrWhiteSpace(_oneSignalSettings.RestApiKey))
        {
            throw new InvalidOperationException("OneSignal REST API Key is not configured");
        }

        if (string.IsNullOrWhiteSpace(_oneSignalSettings.ApiBaseUrl))
        {
            throw new InvalidOperationException("OneSignal API Base URL is not configured");
        }
    }

    /// <summary>
    /// Configures the HTTP client with authentication headers
    /// </summary>
    private void ConfigureHttpClient()
    {
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", _oneSignalSettings.RestApiKey);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }
}
