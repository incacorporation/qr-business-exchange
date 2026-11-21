using Microsoft.JSInterop;
using System.Text.Json;

namespace QRBusinessExchange.Client.Services;

/// <summary>
/// Service for interacting with WebAuthn API for passkey authentication
/// </summary>
public class WebAuthnService
{
    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger<WebAuthnService> _logger;

    public WebAuthnService(IJSRuntime jsRuntime, ILogger<WebAuthnService> logger)
    {
        _jsRuntime = jsRuntime;
        _logger = logger;
    }

    /// <summary>
    /// Check if WebAuthn is supported in the current browser
    /// </summary>
    public async Task<bool> IsSupportedAsync()
    {
        try
        {
            return await _jsRuntime.InvokeAsync<bool>("webAuthnInterop.isSupported");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking WebAuthn support");
            return false;
        }
    }

    /// <summary>
    /// Check if platform authenticator is available (e.g., Windows Hello, Touch ID, Face ID)
    /// </summary>
    public async Task<bool> IsPlatformAuthenticatorAvailableAsync()
    {
        try
        {
            return await _jsRuntime.InvokeAsync<bool>("webAuthnInterop.isPlatformAuthenticatorAvailable");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking platform authenticator availability");
            return false;
        }
    }

    /// <summary>
    /// Register a new passkey credential
    /// </summary>
    /// <param name="options">Registration options from the server</param>
    /// <returns>Credential registration response</returns>
    public async Task<PasskeyRegistrationResponse?> RegisterPasskeyAsync(object options)
    {
        try
        {
            _logger.LogInformation("Starting passkey registration");
            var response = await _jsRuntime.InvokeAsync<JsonElement>("webAuthnInterop.registerPasskey", options);
            
            var result = new PasskeyRegistrationResponse
            {
                Id = response.GetProperty("id").GetString() ?? string.Empty,
                RawId = response.GetProperty("rawId").GetString() ?? string.Empty,
                Type = response.GetProperty("type").GetString() ?? string.Empty,
                Response = new AuthenticatorAttestationResponse
                {
                    ClientDataJSON = response.GetProperty("response").GetProperty("clientDataJSON").GetString() ?? string.Empty,
                    AttestationObject = response.GetProperty("response").GetProperty("attestationObject").GetString() ?? string.Empty
                }
            };

            // Get transports if available
            if (response.TryGetProperty("transports", out var transportsElement))
            {
                var transports = new List<string>();
                foreach (var transport in transportsElement.EnumerateArray())
                {
                    var value = transport.GetString();
                    if (!string.IsNullOrEmpty(value))
                    {
                        transports.Add(value);
                    }
                }
                result.Transports = transports.ToArray();
            }

            _logger.LogInformation("Passkey registration successful");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during passkey registration");
            throw;
        }
    }

    /// <summary>
    /// Authenticate with an existing passkey
    /// </summary>
    /// <param name="options">Authentication options from the server</param>
    /// <returns>Authentication assertion response</returns>
    public async Task<PasskeyAuthenticationResponse?> AuthenticatePasskeyAsync(object options)
    {
        try
        {
            _logger.LogInformation("Starting passkey authentication");
            var response = await _jsRuntime.InvokeAsync<JsonElement>("webAuthnInterop.authenticatePasskey", options);
            
            var result = new PasskeyAuthenticationResponse
            {
                Id = response.GetProperty("id").GetString() ?? string.Empty,
                RawId = response.GetProperty("rawId").GetString() ?? string.Empty,
                Type = response.GetProperty("type").GetString() ?? string.Empty,
                Response = new AuthenticatorAssertionResponse
                {
                    ClientDataJSON = response.GetProperty("response").GetProperty("clientDataJSON").GetString() ?? string.Empty,
                    AuthenticatorData = response.GetProperty("response").GetProperty("authenticatorData").GetString() ?? string.Empty,
                    Signature = response.GetProperty("response").GetProperty("signature").GetString() ?? string.Empty,
                    UserHandle = response.GetProperty("response").TryGetProperty("userHandle", out var userHandle) && !userHandle.ValueKind.Equals(JsonValueKind.Null)
                        ? userHandle.GetString()
                        : null
                }
            };

            _logger.LogInformation("Passkey authentication successful");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during passkey authentication");
            throw;
        }
    }

    /// <summary>
    /// Check if conditional mediation (autofill) is available
    /// </summary>
    public async Task<bool> IsConditionalMediationAvailableAsync()
    {
        try
        {
            return await _jsRuntime.InvokeAsync<bool>("webAuthnInterop.isConditionalMediationAvailable");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking conditional mediation availability");
            return false;
        }
    }

    /// <summary>
    /// Start conditional authentication (autofill)
    /// </summary>
    public async Task<PasskeyAuthenticationResponse?> StartConditionalAuthenticationAsync(object options)
    {
        try
        {
            _logger.LogInformation("Starting conditional authentication");
            var response = await _jsRuntime.InvokeAsync<JsonElement?>("webAuthnInterop.startConditionalAuthentication", options);
            
            if (!response.HasValue || response.Value.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            var responseValue = response.Value;
            var result = new PasskeyAuthenticationResponse
            {
                Id = responseValue.GetProperty("id").GetString() ?? string.Empty,
                RawId = responseValue.GetProperty("rawId").GetString() ?? string.Empty,
                Type = responseValue.GetProperty("type").GetString() ?? string.Empty,
                Response = new AuthenticatorAssertionResponse
                {
                    ClientDataJSON = responseValue.GetProperty("response").GetProperty("clientDataJSON").GetString() ?? string.Empty,
                    AuthenticatorData = responseValue.GetProperty("response").GetProperty("authenticatorData").GetString() ?? string.Empty,
                    Signature = responseValue.GetProperty("response").GetProperty("signature").GetString() ?? string.Empty,
                    UserHandle = responseValue.GetProperty("response").TryGetProperty("userHandle", out var userHandle) && !userHandle.ValueKind.Equals(JsonValueKind.Null)
                        ? userHandle.GetString()
                        : null
                }
            };

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during conditional authentication");
            return null;
        }
    }
}

// Response models
public class PasskeyRegistrationResponse
{
    public string Id { get; set; } = string.Empty;
    public string RawId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public AuthenticatorAttestationResponse Response { get; set; } = new();
    public string[]? Transports { get; set; }
}

public class AuthenticatorAttestationResponse
{
    public string ClientDataJSON { get; set; } = string.Empty;
    public string AttestationObject { get; set; } = string.Empty;
}

public class PasskeyAuthenticationResponse
{
    public string Id { get; set; } = string.Empty;
    public string RawId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public AuthenticatorAssertionResponse Response { get; set; } = new();
}

public class AuthenticatorAssertionResponse
{
    public string ClientDataJSON { get; set; } = string.Empty;
    public string AuthenticatorData { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
    public string? UserHandle { get; set; }
}
