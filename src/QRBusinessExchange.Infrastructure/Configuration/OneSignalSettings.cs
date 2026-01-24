namespace QRBusinessExchange.Infrastructure.Configuration;

/// <summary>
/// Configuration settings for OneSignal push notification service
/// </summary>
public class OneSignalSettings
{
    /// <summary>
    /// Gets or sets the OneSignal application ID
    /// </summary>
    /// <remarks>
    /// Obtained from the OneSignal Dashboard for your application.
    /// This is a unique identifier for your OneSignal app.
    /// </remarks>
    public string AppId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the OneSignal REST API key
    /// </summary>
    /// <remarks>
    /// Obtained from the OneSignal Dashboard under Settings > Keys & IDs.
    /// Should be stored securely and never exposed to clients.
    /// </remarks>
    public string RestApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the OneSignal API base URL
    /// </summary>
    /// <remarks>
    /// Default: "https://onesignal.com/api/v1"
    /// </remarks>
    public string ApiBaseUrl { get; set; } = "https://onesignal.com/api/v1";
}
