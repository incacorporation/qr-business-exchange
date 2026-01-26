using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace QRBusinessExchange.Web.Services;

/// <summary>
/// Service to detect if component is in interactive render mode
/// Prevents JavaScript interop calls during SSR/prerendering
/// </summary>
public class RenderModeService
{
    private readonly IJSRuntime _jsRuntime;
    private bool? _isInteractive;

    public RenderModeService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    /// <summary>
    /// Checks if the current render mode supports JavaScript interop
    /// </summary>
    public async Task<bool> IsInteractiveAsync()
    {
        // Cache the result
        if (_isInteractive.HasValue)
            return _isInteractive.Value;

        try
        {
            // Try to execute a simple JS call
            // This will fail during SSR/prerendering
            await _jsRuntime.InvokeVoidAsync("eval", "void(0)");
            _isInteractive = true;
            return true;
        }
        catch (InvalidOperationException)
        {
            // JS interop not available (SSR/prerendering)
            _isInteractive = false;
            return false;
        }
        catch (JSDisconnectedException)
        {
            // Circuit disconnected
            _isInteractive = false;
            return false;
        }
        catch
        {
            // Any other error - assume not interactive
            _isInteractive = false;
            return false;
        }
    }

    /// <summary>
    /// Resets the interactive state check
    /// </summary>
    public void Reset()
    {
        _isInteractive = null;
    }
}
