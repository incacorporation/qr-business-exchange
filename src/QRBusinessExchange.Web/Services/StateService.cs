using QRBusinessExchange.Core.DTOs;
using Microsoft.JSInterop;

namespace QRBusinessExchange.Web.Services;

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
            // Try to restore IsAuthenticated from browser storage
            var storedAuth = await _jsRuntime.InvokeAsync<bool?>("localStorage.getItem", "isAuthenticated");
            if (storedAuth.HasValue)
            {
                _isAuthenticated = storedAuth.Value;
                Console.WriteLine($"StateService: Restored IsAuthenticated = {_isAuthenticated} from localStorage");
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
            // Persist to browser storage
            try
            {
                _ = _jsRuntime.InvokeVoidAsync("localStorage.setItem", "isAuthenticated", value.ToString().ToLower());
                Console.WriteLine($"StateService: Saved IsAuthenticated = {value} to localStorage");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"StateService: Failed to save to localStorage: {ex.Message}");
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
        }
        catch { }
        
        NotifyStateChanged();
    }

    public void Reset()
    {
        _currentUser = null;
        _currentSubscription = null;
        _isPingMeEnabled = false;
        _isAuthenticated = false;
        NotifyStateChanged();
    }
}
