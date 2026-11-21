using Microsoft.JSInterop;

namespace QRBusinessExchange.Client.Services;

/// <summary>
/// Service for PWA functionality including service worker management and install prompts
/// </summary>
public class PwaService
{
    private readonly IJSRuntime _jsRuntime;

    public PwaService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    /// <summary>
    /// Check if the app is currently installed as PWA
    /// </summary>
    public async Task<bool> IsInstalledAsync()
    {
        try
        {
            return await _jsRuntime.InvokeAsync<bool>("pwaService.isInstalled");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error checking PWA install status: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Update the service worker to latest version
    /// </summary>
    public async Task UpdateServiceWorkerAsync()
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("pwaService.update");
            Console.WriteLine("Service worker update initiated");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating service worker: {ex.Message}");
        }
    }

    /// <summary>
    /// Request notification permission from user
    /// </summary>
    public async Task<string> RequestNotificationPermissionAsync()
    {
        try
        {
            var permission = await _jsRuntime.InvokeAsync<string>("pwaService.requestNotificationPermission");
            Console.WriteLine($"Notification permission: {permission}");
            return permission;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error requesting notification permission: {ex.Message}");
            return "error";
        }
    }

    /// <summary>
    /// Show a notification
    /// </summary>
    public async Task ShowNotificationAsync(string title, string body, string? icon = null)
    {
        try
        {
            var options = new
            {
                body = body,
                icon = icon ?? "/icon-192.svg",
                badge = "/icon-192.svg",
                vibrate = new[] { 200, 100, 200 }
            };

            await _jsRuntime.InvokeVoidAsync("pwaService.showNotification", title, options);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error showing notification: {ex.Message}");
        }
    }

    /// <summary>
    /// Clear all cached data
    /// </summary>
    public async Task ClearCacheAsync()
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("pwaService.clearCache");
            Console.WriteLine("Cache cleared successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error clearing cache: {ex.Message}");
        }
    }

    /// <summary>
    /// Get cache storage information
    /// </summary>
    public async Task<CacheInfo?> GetCacheInfoAsync()
    {
        try
        {
            var info = await _jsRuntime.InvokeAsync<CacheInfo>("pwaService.getCacheSize");
            return info;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting cache info: {ex.Message}");
            return null;
        }
    }
}

/// <summary>
/// Cache storage information
/// </summary>
public class CacheInfo
{
    public long Usage { get; set; }
    public long Quota { get; set; }
    public string UsageInMB { get; set; } = string.Empty;
    public string QuotaInMB { get; set; } = string.Empty;
    public string PercentUsed { get; set; } = string.Empty;
}
