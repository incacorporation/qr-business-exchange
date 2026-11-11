using System.Formats.Cbor;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using QRBusinessExchange.Core.DTOs;
using QRBusinessExchange.Core.Entities;
using QRBusinessExchange.Core.Interfaces;

namespace QRBusinessExchange.Infrastructure.Services;

/// <summary>
/// Service for handling WebAuthn/FIDO2 passkey operations.
/// </summary>
/// <remarks>
/// Challenges are kept in <see cref="IMemoryCache"/> (a singleton) so they survive across
/// requests even though this service is scoped. For a multi-instance deployment, swap the
/// cache for a distributed store.
/// </remarks>
public class WebAuthnService
{
    private const int ChallengeExpiryMinutes = 5;
    private const string CachePrefix = "webauthn:challenge:";

    // Authenticator data flags (WebAuthn §6.1)
    private const byte FlagUserPresent = 0x01;
    private const byte FlagUserVerified = 0x04;
    private const byte FlagAttestedCredentialData = 0x40;

    private readonly IPasskeyRepository _passkeyRepository;
    private readonly IUserRepository _userRepository;
    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _cache;

    private sealed record ChallengeEntry(string Challenge, string? Email, string? Purpose);

    public WebAuthnService(
        IPasskeyRepository passkeyRepository,
        IUserRepository userRepository,
        IConfiguration configuration,
        IMemoryCache cache)
    {
        _passkeyRepository = passkeyRepository;
        _userRepository = userRepository;
        _configuration = configuration;
        _cache = cache;
    }

    private string RpId => _configuration["WebAuthn:RpId"] ?? "localhost";
    private string RpName => _configuration["WebAuthn:RpName"] ?? "QR Business Exchange";

    /// <summary>
    /// Generate options for passkey registration
    /// </summary>
    public async Task<PasskeyRegistrationOptionsDto> GenerateRegistrationOptionsAsync(string email, string? deviceName, string purpose = "register")
    {
        var user = await _userRepository.GetByEmailAsync(email)
            ?? throw new InvalidOperationException("User not found");

        var (challengeKey, challenge) = StoreChallenge(email, purpose);

        var existingCredentials = await _passkeyRepository.GetByUserIdAsync(user.UserId);
        var excludeCredentials = existingCredentials.Select(c => new CredentialDescriptorDto
        {
            Type = "public-key",
            Id = Base64UrlEncode(c.CredentialId),
            Transports = new List<string> { "internal", "hybrid" }
        }).ToList();

        return new PasskeyRegistrationOptionsDto
        {
            Challenge = $"{challengeKey}:{challenge}",
            Rp = new RelyingPartyDto { Id = RpId, Name = RpName },
            User = new UserDto
            {
                Id = Base64UrlEncode(user.UserId.ToByteArray()),
                Name = user.Email,
                DisplayName = $"{user.FirstName} {user.LastName}"
            },
            PubKeyCredParams = new List<PubKeyCredParamDto>
            {
                new() { Type = "public-key", Alg = -7 },   // ES256
                new() { Type = "public-key", Alg = -257 }  // RS256
            },
            AuthenticatorSelection = new AuthenticatorSelectionDto
            {
                // Leave attachment unset so phones / security keys (cross-platform) also work
                AuthenticatorAttachment = null,
                RequireResidentKey = true,
                ResidentKey = "required",
                UserVerification = "required"
            },
            Timeout = 60000,
            Attestation = "none",
            ExcludeCredentials = excludeCredentials.Any() ? excludeCredentials : null
        };
    }

    /// <summary>
    /// Complete passkey registration
    /// </summary>
    public async Task<bool> CompleteRegistrationAsync(string email, string challengeString, CredentialResponseDto credential, string? deviceName, string purpose = "register")
    {
        var expectedChallenge = ConsumeChallenge(challengeString, email, purpose);

        var user = await _userRepository.GetByEmailAsync(email)
            ?? throw new InvalidOperationException("User not found");

        try
        {
            var attestationObject = DecodeBase64Url(credential.Response.AttestationObject);
            var clientDataJSON = DecodeBase64Url(credential.Response.ClientDataJSON);

            VerifyClientData(clientDataJSON, expectedChallenge, "webauthn.create");

            var authData = ReadAuthDataFromAttestation(attestationObject);
            VerifyRpIdHashAndFlags(authData, requireAttestedData: true);

            var (credentialId, publicKey, counter) = ParseAttestedCredentialData(authData);

            if (await _passkeyRepository.GetByCredentialIdAsync(credentialId) != null)
            {
                throw new InvalidOperationException("This passkey is already registered");
            }

            await _passkeyRepository.CreateAsync(new PasskeyCredential
            {
                CredentialId = credentialId,
                UserId = user.UserId,
                PublicKey = publicKey,
                Counter = (int)counter,
                DeviceName = string.IsNullOrWhiteSpace(deviceName) ? "Unknown Device" : deviceName,
                CreatedAt = DateTime.UtcNow
            });

            return true;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to complete registration: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Generate options for passkey authentication.
    /// </summary>
    /// <param name="email">Optional; restricts allowed credentials to that user's passkeys.</param>
    /// <param name="purpose">
    /// Optional binding (e.g. "qr-approve:{sessionId}") so a challenge issued for one
    /// operation cannot be replayed against another.
    /// </param>
    public async Task<PasskeyAuthenticationOptionsDto> GenerateAuthenticationOptionsAsync(string? email = null, string? purpose = null)
    {
        var (challengeKey, challenge) = StoreChallenge(email, purpose ?? "login");

        List<CredentialDescriptorDto>? allowCredentials = null;

        if (!string.IsNullOrEmpty(email))
        {
            var user = await _userRepository.GetByEmailAsync(email);
            if (user != null)
            {
                var credentials = await _passkeyRepository.GetByUserIdAsync(user.UserId);
                allowCredentials = credentials.Select(c => new CredentialDescriptorDto
                {
                    Type = "public-key",
                    Id = Base64UrlEncode(c.CredentialId),
                    Transports = new List<string> { "internal", "hybrid" }
                }).ToList();
            }
        }

        return new PasskeyAuthenticationOptionsDto
        {
            Challenge = $"{challengeKey}:{challenge}",
            RpId = RpId,
            Timeout = 60000,
            UserVerification = "required",
            AllowCredentials = allowCredentials is { Count: > 0 } ? allowCredentials : null
        };
    }

    /// <summary>
    /// Complete passkey authentication. Returns the user that owns the credential.
    /// </summary>
    public async Task<(bool Success, Guid? UserId)> CompleteAuthenticationAsync(
        string challengeString,
        AssertionResponseDto assertion,
        string? purpose = null)
    {
        var expectedChallenge = ConsumeChallenge(challengeString, email: null, purpose: purpose ?? "login");

        try
        {
            var credentialId = DecodeBase64Url(assertion.RawId);

            var credential = await _passkeyRepository.GetByCredentialIdAsync(credentialId)
                ?? throw new InvalidOperationException("Credential not found");

            var clientDataJSON = DecodeBase64Url(assertion.Response.ClientDataJSON);
            var authenticatorData = DecodeBase64Url(assertion.Response.AuthenticatorData);
            var signature = DecodeBase64Url(assertion.Response.Signature);

            VerifyClientData(clientDataJSON, expectedChallenge, "webauthn.get");
            VerifyRpIdHashAndFlags(authenticatorData, requireAttestedData: false);

            var counter = ReadSignCount(authenticatorData);

            // Replay protection. Many synced passkeys always report 0, so only enforce
            // a strictly increasing counter when the authenticator actually uses one.
            if ((counter != 0 || credential.Counter != 0) && counter <= (uint)credential.Counter)
            {
                throw new InvalidOperationException("Invalid counter value - possible cloned authenticator");
            }

            var dataToVerify = authenticatorData.Concat(SHA256.HashData(clientDataJSON)).ToArray();
            if (!VerifySignature(credential.PublicKey, dataToVerify, signature))
            {
                throw new InvalidOperationException("Signature verification failed");
            }

            credential.Counter = (int)counter;
            credential.LastUsed = DateTime.UtcNow;
            await _passkeyRepository.UpdateAsync(credential);

            return (true, credential.UserId);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to complete authentication: {ex.Message}", ex);
        }
    }

    // -----------------------------------------------------------------------
    // Challenge store
    // -----------------------------------------------------------------------

    private (string Key, string Challenge) StoreChallenge(string? email, string purpose)
    {
        var challenge = Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var key = Guid.NewGuid().ToString("N");
        _cache.Set(CachePrefix + key, new ChallengeEntry(challenge, email, purpose),
            TimeSpan.FromMinutes(ChallengeExpiryMinutes));
        return (key, challenge);
    }

    /// <summary>
    /// Validates and removes a challenge (single use). Returns the raw challenge.
    /// </summary>
    private string ConsumeChallenge(string challengeString, string? email, string purpose)
    {
        var parts = challengeString.Split(':');
        if (parts.Length != 2)
        {
            throw new InvalidOperationException("Invalid challenge format");
        }

        var cacheKey = CachePrefix + parts[0];
        if (!_cache.TryGetValue(cacheKey, out ChallengeEntry? entry) || entry == null)
        {
            throw new InvalidOperationException("Challenge not found or expired");
        }

        _cache.Remove(cacheKey);

        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(entry.Challenge), Encoding.ASCII.GetBytes(parts[1])))
        {
            throw new InvalidOperationException("Challenge mismatch");
        }

        if (email != null && !string.Equals(entry.Email, email, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Challenge mismatch");
        }

        if (!string.Equals(entry.Purpose, purpose, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Challenge was issued for a different operation");
        }

        return entry.Challenge;
    }

    // -----------------------------------------------------------------------
    // Verification helpers
    // -----------------------------------------------------------------------

    private IReadOnlyCollection<string> AllowedOrigins
    {
        get
        {
            var configured = _configuration.GetSection("WebAuthn:Origins").Get<string[]>();
            if (configured is { Length: > 0 })
            {
                return configured.Select(o => o.TrimEnd('/')).ToArray();
            }

            return new[] { $"https://{RpId}" };
        }
    }

    private void VerifyClientData(byte[] clientDataJSON, string expectedChallenge, string expectedType)
    {
        var clientData = JsonSerializer.Deserialize<JsonElement>(Encoding.UTF8.GetString(clientDataJSON));

        if (clientData.GetProperty("type").GetString() != expectedType)
        {
            throw new InvalidOperationException("Invalid client data type");
        }

        if (clientData.GetProperty("challenge").GetString() != expectedChallenge)
        {
            throw new InvalidOperationException("Challenge mismatch in client data");
        }

        var origin = clientData.GetProperty("origin").GetString()?.TrimEnd('/');
        var allowed = AllowedOrigins;
        var isLocalhostDev = RpId == "localhost" && origin != null &&
                             Uri.TryCreate(origin, UriKind.Absolute, out var o) && o.Host == "localhost";

        if (origin == null || (!allowed.Contains(origin, StringComparer.OrdinalIgnoreCase) && !isLocalhostDev))
        {
            throw new InvalidOperationException($"Origin '{origin}' is not allowed");
        }
    }

    private void VerifyRpIdHashAndFlags(byte[] authData, bool requireAttestedData)
    {
        if (authData.Length < 37)
        {
            throw new InvalidOperationException("Authenticator data too short");
        }

        var expectedRpIdHash = SHA256.HashData(Encoding.UTF8.GetBytes(RpId));
        if (!CryptographicOperations.FixedTimeEquals(authData.AsSpan(0, 32), expectedRpIdHash))
        {
            throw new InvalidOperationException("RP ID hash mismatch");
        }

        var flags = authData[32];
        if ((flags & FlagUserPresent) == 0)
        {
            throw new InvalidOperationException("User presence flag not set");
        }

        if ((flags & FlagUserVerified) == 0)
        {
            throw new InvalidOperationException("User verification is required");
        }

        if (requireAttestedData && (flags & FlagAttestedCredentialData) == 0)
        {
            throw new InvalidOperationException("Attested credential data missing");
        }
    }

    private static uint ReadSignCount(byte[] authData) =>
        System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(authData.AsSpan(33, 4));

    /// <summary>
    /// Reads "authData" from a CBOR attestation object ({ fmt, attStmt, authData }).
    /// </summary>
    private static byte[] ReadAuthDataFromAttestation(byte[] attestationObject)
    {
        var reader = new CborReader(attestationObject);
        reader.ReadStartMap();

        byte[]? authData = null;
        while (reader.PeekState() != CborReaderState.EndMap)
        {
            var key = reader.ReadTextString();
            if (key == "authData")
            {
                authData = reader.ReadByteString();
            }
            else
            {
                reader.SkipValue(); // fmt (text) and attStmt (map)
            }
        }

        reader.ReadEndMap();
        return authData ?? throw new InvalidOperationException("Auth data not found in attestation");
    }

    /// <summary>
    /// Parses rpIdHash(32) | flags(1) | signCount(4) | aaguid(16) | credIdLen(2) | credId | COSE key | [extensions]
    /// </summary>
    private static (byte[] CredentialId, byte[] PublicKey, uint Counter) ParseAttestedCredentialData(byte[] authData)
    {
        var counter = ReadSignCount(authData);

        var offset = 37 + 16; // skip aaguid
        var credentialIdLength = System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(authData.AsSpan(offset, 2));
        offset += 2;

        var credentialId = authData.AsSpan(offset, credentialIdLength).ToArray();
        offset += credentialIdLength;

        // Measure exactly one CBOR item so trailing extension data is not stored as part of the key
        var remaining = authData.AsMemory(offset);
        var reader = new CborReader(remaining, CborConformanceMode.Lax, allowMultipleRootLevelValues: true);
        reader.SkipValue();
        var keyLength = remaining.Length - reader.BytesRemaining;

        return (credentialId, remaining[..keyLength].ToArray(), counter);
    }

    private static bool VerifySignature(byte[] publicKeyCbor, byte[] data, byte[] signature)
    {
        try
        {
            var reader = new CborReader(publicKeyCbor, CborConformanceMode.Lax);
            reader.ReadStartMap();

            int? kty = null, alg = null;
            byte[]? x = null, y = null, n = null, e = null;
            var rawNegOne = (byte[]?)null;

            while (reader.PeekState() != CborReaderState.EndMap)
            {
                var key = reader.ReadInt32();
                switch (key)
                {
                    case 1: kty = reader.ReadInt32(); break;
                    case 3: alg = reader.ReadInt32(); break;
                    case -1:
                        // EC2: crv (int). RSA: n (bytes).
                        if (reader.PeekState() == CborReaderState.ByteString) rawNegOne = reader.ReadByteString();
                        else reader.SkipValue();
                        break;
                    case -2:
                        // EC2: x. RSA: e.
                        var v2 = reader.ReadByteString();
                        x = v2; e = v2;
                        break;
                    case -3: y = reader.ReadByteString(); break;
                    default: reader.SkipValue(); break;
                }
            }

            reader.ReadEndMap();
            n = rawNegOne;

            if (alg == -7 && kty == 2) // ES256 / EC2 P-256
            {
                if (x == null || y == null) return false;

                using var ecdsa = ECDsa.Create(new ECParameters
                {
                    Curve = ECCurve.NamedCurves.nistP256,
                    Q = new ECPoint { X = x, Y = y }
                });

                // WebAuthn ECDSA signatures are ASN.1 DER encoded
                return ecdsa.VerifyData(data, signature, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);
            }

            if (alg == -257 && kty == 3) // RS256
            {
                if (n == null || e == null) return false;

                using var rsa = RSA.Create(new RSAParameters { Modulus = n, Exponent = e });
                return rsa.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    // -----------------------------------------------------------------------
    // Encoding helpers
    // -----------------------------------------------------------------------

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] DecodeBase64Url(string base64Url)
    {
        var base64 = base64Url.Replace('-', '+').Replace('_', '/');
        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }
        return Convert.FromBase64String(base64);
    }
}
