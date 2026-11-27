namespace QRBusinessExchange.Client.Services;

/// <summary>Turns a user-agent string into "Chrome on Android" style text.</summary>
public static class DeviceDescriber
{
    public static string Describe(string? userAgent)
    {
        if (string.IsNullOrEmpty(userAgent)) return "Unknown device";

        var os = userAgent switch
        {
            var ua when ua.Contains("iPhone") => "iPhone",
            var ua when ua.Contains("iPad") => "iPad",
            var ua when ua.Contains("Android") => "Android",
            var ua when ua.Contains("Windows") => "Windows",
            var ua when ua.Contains("Mac OS X") => "macOS",
            var ua when ua.Contains("Linux") => "Linux",
            _ => "Unknown OS"
        };

        var browser = userAgent switch
        {
            var ua when ua.Contains("Edg") => "Edge",
            var ua when ua.Contains("SamsungBrowser/") => "Samsung Internet",
            var ua when ua.Contains("CriOS/") || ua.Contains("Chrome/") => "Chrome",
            var ua when ua.Contains("FxiOS/") || ua.Contains("Firefox/") => "Firefox",
            var ua when ua.Contains("Safari/") => "Safari",
            _ => "Browser"
        };

        return $"{browser} on {os}";
    }

    /// <summary>Default passkey name for this device, e.g. "Android phone".</summary>
    public static string DefaultDeviceName(string? userAgent) => userAgent switch
    {
        null or "" => "Linked phone",
        var ua when ua.Contains("iPhone") => "iPhone",
        var ua when ua.Contains("iPad") => "iPad",
        var ua when ua.Contains("Android") => "Android phone",
        _ => "Linked phone"
    };
}
