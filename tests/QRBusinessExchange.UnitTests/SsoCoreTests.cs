using QRBusinessExchange.Infrastructure.Services;

namespace QRBusinessExchange.UnitTests;

public class SsoCoreTests
{
    [Fact]
    public void PkceS256_MatchesRfc7636TestVector()
    {
        // RFC 7636 Appendix B
        Assert.Equal(
            "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM",
            SsoCrypto.PkceS256("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk"));
    }

    [Fact]
    public void SecretHash_MatchesOnlyTheOriginalValue()
    {
        var secret = SsoCrypto.NewSecret();
        var hash = SsoCrypto.Hash(secret);

        Assert.True(SsoCrypto.Matches(secret, hash));
        Assert.False(SsoCrypto.Matches(secret + "x", hash));
        Assert.False(SsoCrypto.Matches(null, hash));
        Assert.False(SsoCrypto.Matches(secret, null));
    }

    [Theory]
    [InlineData("https://app.example.com/callback")]
    [InlineData("http://localhost:5000/signin")]
    [InlineData("http://127.0.0.1:8080/cb")]
    [InlineData("myapp://auth/callback")]
    public void ValidateRedirectUris_AcceptsSafeUris(string uri)
    {
        Assert.Equal(new[] { uri }, BusinessService.ValidateRedirectUris(new[] { uri }));
    }

    [Theory]
    [InlineData("http://app.example.com/callback")]   // plain http off-loopback
    [InlineData("https://app.example.com/cb#frag")]    // fragment
    [InlineData("javascript:alert(1)")]                // script scheme
    [InlineData("/relative/path")]                     // not absolute
    public void ValidateRedirectUris_RejectsUnsafeUris(string uri)
    {
        Assert.Throws<SsoException>(() => BusinessService.ValidateRedirectUris(new[] { uri }));
    }

    [Fact]
    public void SigningKeyProvider_PersistsKeyAndPublishesOnlyPublicJwk()
    {
        var path = Path.Combine(Path.GetTempPath(), $"sso-key-{Guid.NewGuid():N}", "key.pem");
        try
        {
            string kid;
            using (var first = new SsoSigningKeyProvider(path))
            {
                kid = first.KeyId;
                Assert.Equal("RS256", first.Jwk["alg"]);
                Assert.False(first.Jwk.ContainsKey("d")); // no private exponent
            }

            using var reloaded = new SsoSigningKeyProvider(path);
            Assert.Equal(kid, reloaded.KeyId);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }
}
