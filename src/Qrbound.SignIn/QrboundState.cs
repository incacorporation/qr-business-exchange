using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;

namespace Qrbound.SignIn;

/// <summary>Values kept between starting a redirect sign-in and the callback.</summary>
public sealed class QrboundPendingRedirect
{
    public string State { get; set; } = string.Empty;
    public string Nonce { get; set; } = string.Empty;
    public string CodeVerifier { get; set; } = string.Empty;
    public string? ReturnUrl { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>Stores <see cref="QrboundPendingRedirect"/> across the full-page redirect.</summary>
public interface IQrboundStateStore
{
    ValueTask SaveAsync(QrboundPendingRedirect pending);

    /// <summary>Returns and removes the entry (single use).</summary>
    ValueTask<QrboundPendingRedirect?> TakeAsync(string state);
}

/// <summary>Default store: the browser's sessionStorage (per tab, cleared when the tab closes).</summary>
public sealed class SessionStorageQrboundStateStore(IJSRuntime js) : IQrboundStateStore
{
    private const string Prefix = "qrbound.pending.";

    public ValueTask SaveAsync(QrboundPendingRedirect pending) =>
        js.InvokeVoidAsync("sessionStorage.setItem", Prefix + pending.State, JsonSerializer.Serialize(pending));

    public async ValueTask<QrboundPendingRedirect?> TakeAsync(string state)
    {
        var json = await js.InvokeAsync<string?>("sessionStorage.getItem", Prefix + state);
        await js.InvokeVoidAsync("sessionStorage.removeItem", Prefix + state);
        return string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<QrboundPendingRedirect>(json);
    }
}

/// <summary>
/// Who is signed in through qrbound in this app (per browser tab / circuit). Kept in memory:
/// a page reload signs the user out unless your app persists it (for example with a backend cookie).
/// </summary>
public sealed class QrboundAuthState
{
    public QrboundUser? User { get; private set; }

    /// <summary>The access token for calling APIs that accept qrbound tokens (Direct mode only).</summary>
    public string? AccessToken { get; private set; }

    public bool IsSignedIn => User != null && User.ExpiresAt > DateTimeOffset.UtcNow;

    public event Action? Changed;

    public void SignIn(QrboundSignInResult result)
    {
        User = result.User;
        AccessToken = result.Tokens?.AccessToken;
        Changed?.Invoke();
    }

    public void SignOut()
    {
        User = null;
        AccessToken = null;
        Changed?.Invoke();
    }
}

/// <summary>
/// Makes <c>&lt;AuthorizeView&gt;</c> and <c>[Authorize]</c> work with <see cref="QrboundAuthState"/>.
/// Registered by <c>AddQrboundSignIn</c> only when <c>useAuthenticationStateProvider</c> is true.
/// </summary>
public sealed class QrboundAuthenticationStateProvider : AuthenticationStateProvider, IDisposable
{
    private static readonly ClaimsPrincipal Anonymous = new(new ClaimsIdentity());
    private readonly QrboundAuthState _state;

    public QrboundAuthenticationStateProvider(QrboundAuthState state)
    {
        _state = state;
        _state.Changed += OnChanged;
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
        Task.FromResult(new AuthenticationState(_state.IsSignedIn ? _state.User!.ToPrincipal() : Anonymous));

    private void OnChanged() => NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());

    public void Dispose() => _state.Changed -= OnChanged;
}
