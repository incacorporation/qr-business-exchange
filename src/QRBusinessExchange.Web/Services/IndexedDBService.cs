using Microsoft.JSInterop;
using System.Text.Json;

namespace QRBusinessExchange.Web.Services;

public class IndexedDBService : IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private bool _disposed = false;

    public IndexedDBService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    private bool IsJSRuntimeAvailable()
    {
        try
        {
            // Check if we have an in-process runtime (Blazor Server interactive)
            return _jsRuntime is IJSInProcessRuntime || 
                   !(_jsRuntime.GetType().Name.Contains("Unsupported"));
        }
        catch
        {
            return false;
        }
    }

    public async Task InitializeAsync()
    {
        if (_disposed || !IsJSRuntimeAvailable()) return;

        try
        {
            await _jsRuntime.InvokeVoidAsync("indexedDBInterop.initialize");
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("prerendering"))
        {
            Console.WriteLine("IndexedDB: Skipping - in prerender mode");
        }
        catch (JSDisconnectedException)
        {
            Console.WriteLine("IndexedDB initialization: Circuit disconnected");
        }
        catch (TaskCanceledException)
        {
            Console.WriteLine("IndexedDB initialization: Operation cancelled");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"IndexedDB initialization error: {ex.Message}");
        }
    }

	public async Task<T?> GetItemAsync<T>(string key)
    {
        if (_disposed || !IsJSRuntimeAvailable()) return default;

        try
        {
            var json = await _jsRuntime.InvokeAsync<string>("indexedDBInterop.getItem", key);
            if (string.IsNullOrEmpty(json))
            {
                return default;
            }
            return JsonSerializer.Deserialize<T>(json);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("prerendering"))
        {
            Console.WriteLine("IndexedDB get: Skipping - in prerender mode");
            return default;
        }
        catch (JSDisconnectedException)
        {
            Console.WriteLine("IndexedDB get: Circuit disconnected");
            return default;
        }
        catch (TaskCanceledException)
        {
            Console.WriteLine("IndexedDB get: Operation cancelled");
            return default;
        }
        catch (ObjectDisposedException)
        {
            Console.WriteLine("IndexedDB get: Service disposed");
            return default;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"IndexedDB get error for key '{key}': {ex.Message}");
            return default;
        }
    }

    public async Task SetItemAsync<T>(string key, T value)
    {
        if (_disposed || !IsJSRuntimeAvailable()) return;

        try
        {
            var json = JsonSerializer.Serialize(value);
            await _jsRuntime.InvokeVoidAsync("indexedDBInterop.setItem", key, json);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("prerendering"))
        {
            Console.WriteLine("IndexedDB set: Skipping - in prerender mode");
        }
        catch (JSDisconnectedException)
        {
            Console.WriteLine("IndexedDB set: Circuit disconnected");
        }
        catch (TaskCanceledException)
        {
            Console.WriteLine("IndexedDB set: Operation cancelled");
        }
        catch (ObjectDisposedException)
        {
            Console.WriteLine("IndexedDB set: Service disposed");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"IndexedDB set error for key '{key}': {ex.Message}");
        }
    }

    public async Task RemoveItemAsync(string key)
    {
        if (_disposed || !IsJSRuntimeAvailable()) return;

        try
        {
            await _jsRuntime.InvokeVoidAsync("indexedDBInterop.removeItem", key);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("prerendering"))
        {
            Console.WriteLine("IndexedDB remove: Skipping - in prerender mode");
        }
        catch (JSDisconnectedException)
        {
            Console.WriteLine("IndexedDB remove: Circuit disconnected");
        }
        catch (TaskCanceledException)
        {
            Console.WriteLine("IndexedDB remove: Operation cancelled");
        }
        catch (ObjectDisposedException)
        {
            Console.WriteLine("IndexedDB remove: Service disposed");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"IndexedDB remove error for key '{key}': {ex.Message}");
        }
    }

    public async Task ClearAsync()
    {
        if (_disposed || !IsJSRuntimeAvailable()) return;

        try
        {
            await _jsRuntime.InvokeVoidAsync("indexedDBInterop.clear");
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("prerendering"))
        {
            Console.WriteLine("IndexedDB clear: Skipping - in prerender mode");
        }
        catch (JSDisconnectedException)
        {
            Console.WriteLine("IndexedDB clear: Circuit disconnected");
        }
        catch (TaskCanceledException)
        {
            Console.WriteLine("IndexedDB clear: Operation cancelled");
        }
        catch (ObjectDisposedException)
        {
            Console.WriteLine("IndexedDB clear: Service disposed");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"IndexedDB clear error: {ex.Message}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await Task.CompletedTask;
    }
}


