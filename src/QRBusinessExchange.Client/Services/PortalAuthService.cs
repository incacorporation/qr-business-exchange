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

    /// <summary>Stores the portal session (token + profile).</summary>
    public async Task SaveSessionAsync(AuthResponse auth)
    {
        await _storage.SetItemAsync("authToken", auth.AccessToken);
        await _storage.SetItemAsync("userId", auth.User.UserId.ToString());
        _state.CurrentUser = auth.User;
        _state.IsAuthenticated = true;
    }

    public async Task<bool> HasSessionAsync()
    {
        await _state.InitializeAsync();
        var token = await _storage.GetItemAsync<string>("authToken");
        return _state.IsAuthenticated && !string.IsNullOrEmpty(token);
    }

    public Task SignOutAsync() => _state.ResetAsync();

    /// <summary>Friendly text for exceptions thrown by the browser's WebAuthn API.</summary>
    public static string Describe(Exception ex)
    {
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
