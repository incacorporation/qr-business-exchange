using Microsoft.JSInterop;

namespace QRBusinessExchange.Client.Services;

public class IndexedDBService
{
    private readonly IJSRuntime _jsRuntime;

    public IndexedDBService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task SetItemAsync<T>(string key, T value)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", key, System.Text.Json.JsonSerializer.Serialize(value));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"IndexedDB SetItem Error: {ex.Message}");
        }
    }

    public async Task<T?> GetItemAsync<T>(string key)
    {
        try
        {
            var json = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", key);
            if (string.IsNullOrEmpty(json))
                return default;
                
            return System.Text.Json.JsonSerializer.Deserialize<T>(json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"IndexedDB GetItem Error: {ex.Message}");
            return default;
        }
    }

    public async Task RemoveItemAsync(string key)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", key);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"IndexedDB RemoveItem Error: {ex.Message}");
        }
    }
}
