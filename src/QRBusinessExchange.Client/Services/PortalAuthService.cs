using QRBusinessExchange.Core.DTOs;

namespace QRBusinessExchange.Client.Services;

/// <summary>
/// Portal sign-in helpers shared by the login page, the SSO authorize page and the
/// QR approval page: passkey sign-in, passkey step-up assertions and session storage.
/// </summary>
public class PortalAuthService
{
    private readonly ApiService _api;
    private readonly WebAuthnService _webAuthn;
    private readonly StateService _state;
    private readonly IndexedDBService _storage;

    public PortalAuthService(ApiService api, WebAuthnService webAuthn, StateService state, IndexedDBService storage)
    {
        _api = api;
        _webAuthn = webAuthn;
        _state = state;
        _storage = storage;
    }

    /// <summary>
    /// Signs in with a passkey. With no email the browser offers any passkey saved for this site.
    /// </summary>
    public async Task<AuthResponse> SignInWithPasskeyAsync(string? email = null)
    {
        var initResponse = await _api.PostAsync("api/passkey/authenticate/init", new PasskeyAuthenticationInitRequest { Email = email });
        if (!initResponse.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(await _api.ReadErrorAsync(initResponse, "Could not start passkey sign-in"));
        }

        var options = await _api.ParseResponseAsync<PasskeyAuthenticationOptionsDto>(initResponse)
            ?? throw new InvalidOperationException("Invalid passkey options");

        var assertion = await AssertAsync(options);

        var completeResponse = await _api.PostAsync("api/passkey/authenticate/complete", new PasskeyAuthenticationCompleteRequest
        {
            Challenge = options.Challenge,
            Assertion = assertion
        });

        if (!completeResponse.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(await _api.ReadErrorAsync(completeResponse, "Passkey sign-in failed"));
        }

        var auth = await _api.ParseResponseAsync<AuthResponse>(completeResponse)
            ?? throw new InvalidOperationException("Invalid sign-in response");

        await SaveSessionAsync(auth);
        return auth;
    }

    /// <summary>
    /// Runs navigator.credentials.get() for the given options and converts the result.
    /// </summary>
    public async Task<AssertionResponseDto> AssertAsync(PasskeyAuthenticationOptionsDto options)
    {
        var assertion = await _webAuthn.AuthenticatePasskeyAsync(options)
            ?? throw new InvalidOperationException("Passkey prompt was cancelled");

        return new AssertionResponseDto
        {
            Id = assertion.Id,
            RawId = assertion.RawId,
            Type = assertion.Type,
            Response = new AssertionDataDto
            {
                ClientDataJSON = assertion.Response.ClientDataJSON,
                AuthenticatorData = assertion.Response.AuthenticatorData,
                Signature = assertion.Response.Signature,
                UserHandle = assertion.Response.UserHandle
            }
        };
    }

    /// <summary>
    /// Runs navigator.credentials.create() for the given options and converts the result.
    /// </summary>
    public async Task<CredentialResponseDto> CreatePasskeyAsync(PasskeyRegistrationOptionsDto options)
    {
        var credential = await _webAuthn.RegisterPasskeyAsync(options)
            ?? throw new InvalidOperationException("Passkey prompt was cancelled");

        return new CredentialResponseDto
        {
            Id = credential.Id,
            RawId = credential.RawId,
            Type = credential.Type,
            Response = new AttestationResponseDto
            {
                ClientDataJSON = credential.Response.ClientDataJSON,
                AttestationObject = credential.Response.AttestationObject
            },
            Transports = credential.Transports?.ToList()
        };
    }

    /// <summary>
    /// Stores the portal session (token + profile). Every caller signs in with a passkey or the phone camera,
    /// which proves a device is registered, so the login page can lead with the phone camera next time.
    /// </summary>
    public async Task SaveSessionAsync(AuthResponse auth)
    {
        await _storage.SetItemAsync("authToken", auth.AccessToken);
        await _storage.SetItemAsync("userId", auth.User.UserId.ToString());
        _state.CurrentUser = auth.User;
        _state.IsAuthenticated = true;
        await RememberRegisteredDeviceAsync(true);
    }

    // Kept on sign-out on purpose: it describes this browser's user, not the session, and holds no personal data.
    private const string DeviceRegisteredKey = "deviceRegistered";

    /// <summary>
    /// Whether this browser has seen that its user has a registered device (a passkey or linked phone).
    /// The login page can't ask the server before sign-in, so this is remembered locally.
    /// </summary>
    public async Task<bool> HasRegisteredDeviceAsync()
    {
        try
        {
            return await _storage.GetItemAsync<bool>(DeviceRegisteredKey);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Records whether the user has a registered device (true) or none any more (false).</summary>
    public async Task RememberRegisteredDeviceAsync(bool registered)
    {
        if (registered)
        {
            await _storage.SetItemAsync(DeviceRegisteredKey, true);
        }
        else
        {
            await _storage.RemoveItemAsync(DeviceRegisteredKey);
        }
    }

    public async Task<bool> HasSessionAsync()
    {
        await _state.InitializeAsync();
        var token = await _storage.GetItemAsync<string>("authToken");
        return _state.IsAuthenticated && !string.IsNullOrEmpty(token);
    }

    /// <summary>
    /// Whether this browser has a usable portal session: none, one that's still valid, or one whose
    /// token has expired (the user signed in before but must log in again).
    /// </summary>
    public async Task<PortalSessionState> GetSessionStateAsync()
    {
        if (!await HasSessionAsync())
        {
            return PortalSessionState.None;
        }

        var token = await _storage.GetItemAsync<string>("authToken");
        var expiresAt = ReadExpiry(token);
        return expiresAt is null || expiresAt > DateTimeOffset.UtcNow
            ? PortalSessionState.Active
            : PortalSessionState.Expired;
    }

    /// <summary>Reads the "exp" claim of a JWT without validating it (the API does that).</summary>
    private static DateTimeOffset? ReadExpiry(string? token)
    {
        var parts = token?.Split('.');
        if (parts is not { Length: 3 })
        {
            return null;
        }

        try
        {
            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            using var json = System.Text.Json.JsonDocument.Parse(Convert.FromBase64String(payload));
            return json.RootElement.TryGetProperty("exp", out var exp) && exp.TryGetInt64(out var seconds)
                ? DateTimeOffset.FromUnixTimeSeconds(seconds)
                : null;
        }
        catch (Exception ex) when (ex is FormatException or System.Text.Json.JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Reloads the signed-in user's profile from the server. The profile is kept in browser storage from
    /// sign-in, so without this, changes made on another device or tab never show up here.
    /// Returns whether anything changed. Quietly does nothing when offline or signed out.
    /// </summary>
    public async Task<bool> RefreshProfileAsync()
    {
        if (await GetSessionStateAsync() != PortalSessionState.Active || _state.CurrentUser is not { } current)
        {
            return false;
        }

        try
        {
            var response = await _api.GetResponseAsync("api/user/profile");
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            var fresh = await _api.ParseResponseAsync<UserProfileDto>(response);
            if (fresh == null || fresh.UserId != current.UserId)
            {
                return false;
            }

            if (System.Text.Json.JsonSerializer.Serialize(fresh) == System.Text.Json.JsonSerializer.Serialize(current))
            {
                return false;
            }

            _state.CurrentUser = fresh; // saves it and redraws the menu and header
            return true;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// Loads the signed-in user's active plan, so the Plans page and the header badge show it (no active
    /// subscription means Free). Leaves the current value alone if the server can't be reached.
    /// </summary>
    public async Task RefreshSubscriptionAsync()
    {
        if (await GetSessionStateAsync() != PortalSessionState.Active)
        {
            return;
        }

        try
        {
            var response = await _api.GetResponseAsync("api/subscription/current");
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _state.CurrentSubscription = null;
            }
            else if (response.IsSuccessStatusCode)
            {
                _state.CurrentSubscription = await _api.ParseResponseAsync<SubscriptionDto>(response);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            // Offline: keep showing what we had.
        }
    }

    public Task SignOutAsync() => _state.ResetAsync();

    /// <summary>Shown when the qrbound API can't be reached (offline, server down, request timed out).</summary>
    public const string ConnectionProblem = "Can't reach qrbound right now. Check your internet connection and try again.";

    /// <summary>Friendly text for exceptions from the browser's WebAuthn API or from calling the qrbound API.</summary>
    public static string Describe(Exception ex)
    {
        if (ex is HttpRequestException or TaskCanceledException)
        {
            return ConnectionProblem;
        }

        var message = ex.Message;
        if (message.Contains("NotAllowedError", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("cancel", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("abort", StringComparison.OrdinalIgnoreCase))
        {
            return "The passkey prompt was cancelled or timed out.";
        }

        return message;
    }
}

/// <summary>The portal session this browser holds, from <see cref="PortalAuthService.GetSessionStateAsync"/>.</summary>
public enum PortalSessionState
{
    /// <summary>Never signed in here, or signed out.</summary>
    None,

    /// <summary>Signed in with a token that hasn't expired.</summary>
    Active,

    /// <summary>Signed in before, but the token has expired.</summary>
    Expired
}
