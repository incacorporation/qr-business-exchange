using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

namespace QRBusinessExchange.Web.Services;

public class CustomAuthStateProvider : AuthenticationStateProvider
{
    private readonly IndexedDBService _indexedDB;
    private readonly StateService _stateService;

    public CustomAuthStateProvider(IndexedDBService indexedDB, StateService stateService)
    {
        _indexedDB = indexedDB;
        _stateService = stateService;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var token = await _indexedDB.GetItemAsync<string>("authToken");
        
        if (string.IsNullOrEmpty(token))
        {
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }

        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, _stateService.UserFullName),
            new Claim(ClaimTypes.Email, _stateService.CurrentUser?.Email ?? "")
        }, "apiauth");

        var user = new ClaimsPrincipal(identity);
        return new AuthenticationState(user);
    }

    public void NotifyAuthenticationStateChanged()
    {
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }
}
