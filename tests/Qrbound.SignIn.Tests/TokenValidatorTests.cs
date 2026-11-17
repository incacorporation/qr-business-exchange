using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Qrbound.SignIn.Server;

namespace Qrbound.SignIn.Tests;

public class TokenValidatorTests
{
    private readonly TestTokens _tokens = new();

    private QrboundTokenValidator CreateValidator()
    {
        var config = new OpenIdConnectConfiguration { Issuer = TestTokens.Issuer };
        config.SigningKeys.Add(_tokens.Key);

        return new QrboundTokenValidator(
            new StaticConfigurationManager<OpenIdConnectConfiguration>(config),
            Options.Create(new QrboundServerOptions
            {
                Authority = TestTokens.Issuer,
                ClientId = TestTokens.ClientId,
                RedirectUri = "https://app.test/callback"
            }));
    }

    [Fact]
    public async Task Valid_id_token_passes()
    {
        var principal = await CreateValidator().ValidateIdTokenAsync(_tokens.Create(), "n-1");
        Assert.Equal("Test User", principal.Identity!.Name);
        Assert.Equal("11111111-2222-3333-4444-555555555555", principal.FindFirst("sub")!.Value);
        Assert.True(principal.IsInRole("Admin"));
    }

    [Fact]
    public async Task Valid_access_token_passes()
    {
        var principal = await CreateValidator().ValidateAccessTokenAsync(_tokens.Create(tokenUse: "access", nonce: null));
        Assert.Equal("access", principal.FindFirst("token_use")!.Value);
    }

    [Fact]
    public async Task Token_signed_by_another_key_fails()
    {
        var forged = _tokens.Create(signWith: new RsaSecurityKey(RSA.Create(2048)) { KeyId = "k1" });
        await Assert.ThrowsAsync<QrboundTokenException>(() => CreateValidator().ValidateIdTokenAsync(forged, "n-1"));
    }

    [Fact]
    public async Task Tampered_payload_fails()
    {
        var parts = _tokens.Create().Split('.');
        var payload = Encoding.UTF8.GetString(Base64Url.Decode(parts[1])).Replace("\"Admin\"", "\"Owner\"");
        var tampered = $"{parts[0]}.{Base64Url.Encode(Encoding.UTF8.GetBytes(payload))}.{parts[2]}";
        await Assert.ThrowsAsync<QrboundTokenException>(() => CreateValidator().ValidateIdTokenAsync(tampered, "n-1"));
    }

    [Fact]
    public async Task Unsigned_token_fails()
    {
        var parts = _tokens.Create().Split('.');
        var header = Base64Url.Encode(Encoding.UTF8.GetBytes("{\"alg\":\"none\",\"typ\":\"JWT\"}"));
        await Assert.ThrowsAsync<QrboundTokenException>(() =>
            CreateValidator().ValidateIdTokenAsync($"{header}.{parts[1]}.", "n-1"));
    }

    [Fact]
    public async Task Wrong_audience_fails() =>
        await Assert.ThrowsAsync<QrboundTokenException>(() =>
            CreateValidator().ValidateIdTokenAsync(_tokens.Create(audience: "other_app"), "n-1"));

    [Fact]
    public async Task Wrong_issuer_fails() =>
        await Assert.ThrowsAsync<QrboundTokenException>(() =>
            CreateValidator().ValidateIdTokenAsync(_tokens.Create(issuer: "https://evil.test"), "n-1"));

    [Fact]
    public async Task Expired_token_fails() =>
        await Assert.ThrowsAsync<QrboundTokenException>(() =>
            CreateValidator().ValidateIdTokenAsync(_tokens.Create(expires: DateTime.UtcNow.AddMinutes(-10)), "n-1"));

    [Fact]
    public async Task Id_token_is_not_an_access_token() =>
        await Assert.ThrowsAsync<QrboundTokenException>(() =>
            CreateValidator().ValidateAccessTokenAsync(_tokens.Create(tokenUse: "id")));

    [Fact]
    public async Task Nonce_mismatch_fails() =>
        await Assert.ThrowsAsync<QrboundTokenException>(() =>
            CreateValidator().ValidateIdTokenAsync(_tokens.Create(nonce: "other"), "n-1"));

    [Fact]
    public async Task Principal_maps_to_backend_user_and_back()
    {
        var principal = await CreateValidator().ValidateIdTokenAsync(_tokens.Create(), "n-1");
        var user = QrboundServerExtensions.ToUser(principal);
        Assert.Equal("user@example.test", user.Email);
        Assert.Equal("qr", user.AuthMethod);
        Assert.True(user.EmailVerified);

        var back = QrboundBackendUser.From(user).ToUser();
        Assert.Equal(user.Subject, back.Subject);
        Assert.Equal("Admin", back.BusinessRole);
    }
}
