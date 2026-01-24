using QRBusinessExchange.Core.DTOs;

namespace QRBusinessExchange.Web.Services;

public class StateService
{
    private UserProfileDto? _currentUser;
    private SubscriptionDto? _currentSubscription;
    private bool _isPingMeEnabled;

    public event Action? OnStateChanged;

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

    public bool IsAuthenticated => _currentUser != null;

    public string UserFullName => _currentUser != null 
        ? $"{_currentUser.FirstName} {_currentUser.LastName}" 
        : string.Empty;

    public int CreditsRemaining => _currentSubscription?.CreditsAvailable ?? 0;

    public string SubscriptionType => _currentSubscription?.SubscriptionType ?? "Free";

    private void NotifyStateChanged() => OnStateChanged?.Invoke();

    public void Reset()
    {
        _currentUser = null;
        _currentSubscription = null;
        _isPingMeEnabled = false;
        NotifyStateChanged();
    }
}
