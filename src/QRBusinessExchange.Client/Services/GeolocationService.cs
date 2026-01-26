using Microsoft.JSInterop;

namespace QRBusinessExchange.Client.Services;

public class GeolocationService
{
    private readonly IJSRuntime _jsRuntime;

    public GeolocationService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task<GeolocationResult?> GetCurrentPositionAsync()
    {
        try
        {
            // Small delay to ensure JS runtime is fully initialized
            await Task.Delay(100);
            
            var result = await _jsRuntime.InvokeAsync<GeolocationResult>(
                "geolocationInterop.getCurrentPosition");
            
            Console.WriteLine($"Location obtained: {result.Latitude}, {result.Longitude}");
            return result;
        }
        catch (JSException ex)
        {
            Console.WriteLine($"JavaScript error getting location: {ex.Message}");
            return null;
        }
        catch (TaskCanceledException ex)
        {
            Console.WriteLine($"Geolocation request cancelled: {ex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting location: {ex.Message}");
            return null;
        }
    }

    public async Task<string> CheckPermissionAsync()
    {
        try
        {
            var permission = await _jsRuntime.InvokeAsync<string>(
                "geolocationInterop.checkPermission");
            return permission;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error checking permission: {ex.Message}");
            return "unsupported";
        }
    }
}

public class GeolocationResult
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Accuracy { get; set; }
}
