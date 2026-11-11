namespace QRBusinessExchange.Core.DTOs;

/// <summary>
/// Request to initiate passkey registration
/// </summary>
public class PasskeyRegistrationInitRequest
{
    public string Email { get; set; } = string.Empty;
    public string? DeviceName { get; set; }
}

/// <summary>
/// Options for WebAuthn credential creation (sent to client)
/// </summary>
public class PasskeyRegistrationOptionsDto
{
    public string Challenge { get; set; } = string.Empty;
    public RelyingPartyDto Rp { get; set; } = new();
    public UserDto User { get; set; } = new();
    public List<PubKeyCredParamDto> PubKeyCredParams { get; set; } = new();
    public AuthenticatorSelectionDto? AuthenticatorSelection { get; set; }
    public int Timeout { get; set; } = 60000;
    public string Attestation { get; set; } = "none";
    public List<CredentialDescriptorDto>? ExcludeCredentials { get; set; }
}

/// <summary>
/// Complete passkey registration request (from client after credential creation)
/// </summary>
public class PasskeyRegistrationCompleteRequest
{
    public string Email { get; set; } = string.Empty;
    public string Challenge { get; set; } = string.Empty;
    public CredentialResponseDto Credential { get; set; } = new();
    public string? DeviceName { get; set; }
}

/// <summary>
/// Request to initiate passkey authentication
/// </summary>
public class PasskeyAuthenticationInitRequest
{
    public string? Email { get; set; }
}

/// <summary>
/// Options for WebAuthn authentication (sent to client)
/// </summary>
public class PasskeyAuthenticationOptionsDto
{
    public string Challenge { get; set; } = string.Empty;
    public string RpId { get; set; } = string.Empty;
    public int Timeout { get; set; } = 60000;
    public string UserVerification { get; set; } = "required";
    public List<CredentialDescriptorDto>? AllowCredentials { get; set; }
}

/// <summary>
/// Complete passkey authentication request (from client after assertion)
/// </summary>
public class PasskeyAuthenticationCompleteRequest
{
    public string Challenge { get; set; } = string.Empty;
    public AssertionResponseDto Assertion { get; set; } = new();
}

/// <summary>
/// Response after successful passkey authentication
/// </summary>
public class PasskeyAuthenticationResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public UserProfileDto User { get; set; } = new();
}

// Supporting DTOs

public class RelyingPartyDto
{
    public string Name { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
}

public class UserDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}

public class PubKeyCredParamDto
{
    public string Type { get; set; } = "public-key";
    public int Alg { get; set; }
}

public class AuthenticatorSelectionDto
{
    public string? AuthenticatorAttachment { get; set; } // "platform" or "cross-platform"
    public bool RequireResidentKey { get; set; } = true;
    public string ResidentKey { get; set; } = "required"; // "required", "preferred", "discouraged"
    public string UserVerification { get; set; } = "required"; // "required", "preferred", "discouraged"
}

public class CredentialDescriptorDto
{
    public string Type { get; set; } = "public-key";
    public string Id { get; set; } = string.Empty;
    public List<string>? Transports { get; set; }
}

public class CredentialResponseDto
{
    public string Id { get; set; } = string.Empty;
    public string RawId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public AttestationResponseDto Response { get; set; } = new();
    public List<string>? Transports { get; set; }
}

public class AttestationResponseDto
{
    public string ClientDataJSON { get; set; } = string.Empty;
    public string AttestationObject { get; set; } = string.Empty;
}

public class AssertionResponseDto
{
    public string Id { get; set; } = string.Empty;
    public string RawId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public AssertionDataDto Response { get; set; } = new();
}

public class AssertionDataDto
{
    public string ClientDataJSON { get; set; } = string.Empty;
    public string AuthenticatorData { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
    public string? UserHandle { get; set; }
}

/// <summary>
/// List of passkeys for a user
/// </summary>
public class PasskeyListDto
{
    public List<PasskeyInfoDto> Passkeys { get; set; } = new();
}

/// <summary>
/// Information about a registered passkey
/// </summary>
public class PasskeyInfoDto
{
    public string CredentialId { get; set; } = string.Empty;
    public string? DeviceName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastUsed { get; set; }
}
