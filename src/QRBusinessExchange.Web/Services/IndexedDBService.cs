using Microsoft.JSInterop;
using System.Text.Json;

namespace QRBusinessExchange.Web.Services;

public class IndexedDBService
{
    private readonly IJSRuntime _jsRuntime;

    public IndexedDBService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task InitializeAsync()
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("indexedDBInterop.initialize");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"IndexedDB initialization error: {ex.Message}");
        }
    }

    public async Task<T?> GetItemAsync<T>(string key)
    {
        try
        {
            var json = await _jsRuntime.InvokeAsync<string>("indexedDBInterop.getItem", key);
            if (string.IsNullOrEmpty(json))
            {
                return default;
            }
            return JsonSerializer.Deserialize<T>(json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"IndexedDB get error: {ex.Message}");
            return default;
        }
    }

    public async Task SetItemAsync<T>(string key, T value)
    {
        try
        {
            var json = JsonSerializer.Serialize(value);
            await _jsRuntime.InvokeVoidAsync("indexedDBInterop.setItem", key, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"IndexedDB set error: {ex.Message}");
        }
    }

    public async Task RemoveItemAsync(string key)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("indexedDBInterop.removeItem", key);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"IndexedDB remove error: {ex.Message}");
        }
    }

    public async Task ClearAsync()
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("indexedDBInterop.clear");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"IndexedDB clear error: {ex.Message}");
        }
    }
}
