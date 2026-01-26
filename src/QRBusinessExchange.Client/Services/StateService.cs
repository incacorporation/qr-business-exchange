using QRBusinessExchange.Core.DTOs;
using Microsoft.JSInterop;

namespace QRBusinessExchange.Client.Services;

public class StateService
{
    private UserProfileDto? _currentUser;
    private SubscriptionDto? _currentSubscription;
    private bool _isPingMeEnabled;
    private bool _isAuthenticated;
    private readonly IJSRuntime _jsRuntime;
    private bool _isInitialized = false;

    public StateService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public event Action? OnStateChanged;

    public async Task InitializeAsync()
    {
        if (_isInitialized) return;
        
        try
        {
            // Restore IsAuthenticated from localStorage
            var storedAuth = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", "isAuthenticated");
            if (!string.IsNullOrEmpty(storedAuth))
            {
                _isAuthenticated = bool.Parse(storedAuth);
                Console.WriteLine($"StateService: Restored IsAuthenticated = {_isAuthenticated}");
            }

            // Restore CurrentUser from localStorage
            var storedUser = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", "currentUser");
            if (!string.IsNullOrEmpty(storedUser))
            {
                _currentUser = System.Text.Json.JsonSerializer.Deserialize<UserProfileDto>(storedUser);
                Console.WriteLine($"StateService: Restored CurrentUser = {_currentUser?.Email}");
            }

            _isInitialized = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"StateService: Failed to restore from localStorage: {ex.Message}");
            _isInitialized = true;
        }
    }

    public UserProfileDto? CurrentUser
    {
        get => _currentUser;
        set
        {
            _currentUser = value;
            // Persist to localStorage
            try
            {
                if (value != null)
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(value);
                    _ = _jsRuntime.InvokeVoidAsync("localStorage.setItem", "currentUser", json);
                }
                else
                {
                    _ = _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "currentUser");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to save CurrentUser: {ex.Message}");
            }
            NotifyStateChanged();
        }
    }

    public SubscriptionDto? CurrentSubscription
    {
        get => _currentSubscription;
        set
        {
            _currentSubscription = value;
            NotifyStateChanged();
        }
    }

    public bool IsPingMeEnabled
    {
        get => _isPingMeEnabled;
        set
        {
            _isPingMeEnabled = value;
            NotifyStateChanged();
        }
    }

    public bool IsAuthenticated
    {
        get => _isAuthenticated;
        set
        {
            _isAuthenticated = value;
            // Persist to localStorage
            try
            {
                _ = _jsRuntime.InvokeVoidAsync("localStorage.setItem", "isAuthenticated", value.ToString().ToLower());
                Console.WriteLine($"StateService: Saved IsAuthenticated = {value}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to save IsAuthenticated: {ex.Message}");
            }
            NotifyStateChanged();
        }
    }

    public string UserFullName => _currentUser != null 
        ? $"{_currentUser.FirstName} {_currentUser.LastName}" 
        : string.Empty;

    public int CreditsRemaining => _currentSubscription?.CreditsAvailable ?? 0;

    public string SubscriptionType => _currentSubscription?.SubscriptionType ?? "Free";

    private void NotifyStateChanged() => OnStateChanged?.Invoke();

    public async Task ResetAsync()
    {
        _currentUser = null;
        _currentSubscription = null;
        _isPingMeEnabled = false;
        _isAuthenticated = false;
        
        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "isAuthenticated");
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "currentUser");
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "authToken");
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "userId");
        }
        catch { }
        
        NotifyStateChanged();
    }
}
