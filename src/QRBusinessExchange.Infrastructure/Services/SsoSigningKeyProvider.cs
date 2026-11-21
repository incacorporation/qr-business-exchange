using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace QRBusinessExchange.Infrastructure.Services;

/// <summary>
/// Holds the RSA key that signs tokens issued to business applications and exposes
/// its public half as a JWKS document so applications can validate tokens offline.
/// Register as a singleton.
/// </summary>
public sealed class SsoSigningKeyProvider : IDisposable
{
    private readonly RSA _rsa;

    public SsoSigningKeyProvider(string keyPath)
    {
        _rsa = RSA.Create();

        if (File.Exists(keyPath))
        {
            _rsa.ImportFromPem(File.ReadAllText(keyPath));
        }
        else
        {
            _rsa.KeySize = 2048;
            var directory = Path.GetDirectoryName(keyPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
            File.WriteAllText(keyPath, _rsa.ExportRSAPrivateKeyPem());
        }

        var parameters = _rsa.ExportParameters(false);
        KeyId = Base64UrlEncoder.Encode(SHA256.HashData(parameters.Modulus!))[..16];
        SecurityKey = new RsaSecurityKey(_rsa) { KeyId = KeyId };
        SigningCredentials = new SigningCredentials(SecurityKey, SecurityAlgorithms.RsaSha256);

        Jwk = new Dictionary<string, string>
        {
            ["kty"] = "RSA",
            ["use"] = "sig",
            ["alg"] = "RS256",
            ["kid"] = KeyId,
            ["n"] = Base64UrlEncoder.Encode(parameters.Modulus!),
            ["e"] = Base64UrlEncoder.Encode(parameters.Exponent!)
        };
    }

    public string KeyId { get; }

    public RsaSecurityKey SecurityKey { get; }

    public SigningCredentials SigningCredentials { get; }

    /// <summary>Public JSON Web Key (no private material).</summary>
    public IReadOnlyDictionary<string, string> Jwk { get; }

    public void Dispose() => _rsa.Dispose();
}
