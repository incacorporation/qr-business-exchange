using QRBusinessExchange.Infrastructure.Services;

namespace Qrbound.SignIn.Tests;

public class PkceTests
{
    [Fact]
    public void Challenge_matches_RFC7636_appendix_B()
    {
        Assert.Equal("E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM",
            Pkce.CreateChallenge("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk"));
    }

    [Fact]
    public void Challenge_matches_the_qrbound_API()
    {
        for (var i = 0; i < 20; i++)
        {
            var verifier = Pkce.CreateVerifier();
            Assert.Equal(SsoCrypto.PkceS256(verifier), Pkce.CreateChallenge(verifier));
        }
    }

    [Fact]
    public void Verifier_is_43_unreserved_chars_and_unique()
    {
        var a = Pkce.CreateVerifier();
        var b = Pkce.CreateVerifier();
        Assert.Equal(43, a.Length);
        Assert.True(Pkce.IsValidVerifier(a));
        Assert.NotEqual(a, b);
        Assert.Matches("^[A-Za-z0-9_-]+$", a);
    }

    [Fact]
    public void Challenge_is_within_the_length_the_API_accepts()
    {
        var challenge = Pkce.CreateChallenge(Pkce.CreateVerifier());
        Assert.InRange(challenge.Length, 43, 128);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("too-short")]
    [InlineData("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjX!")]
    [InlineData("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjX=")]
    public void Invalid_verifiers_are_rejected(string? verifier)
    {
        Assert.False(Pkce.IsValidVerifier(verifier));
        if (verifier != null) Assert.Throws<ArgumentException>(() => Pkce.CreateChallenge(verifier));
    }

    [Fact]
    public void Max_length_verifier_is_valid_but_129_is_not()
    {
        Assert.True(Pkce.IsValidVerifier(new string('a', 128)));
        Assert.False(Pkce.IsValidVerifier(new string('a', 129)));
    }

    [Fact]
    public void Verify_accepts_only_the_matching_pair()
    {
        var verifier = Pkce.CreateVerifier();
        var challenge = Pkce.CreateChallenge(verifier);
        Assert.True(Pkce.Verify(verifier, challenge));
        Assert.False(Pkce.Verify(Pkce.CreateVerifier(), challenge));
        Assert.False(Pkce.Verify(verifier, ""));
    }

    [Theory]
    [InlineData(new byte[0])]
    [InlineData(new byte[] { 0xfb })]
    [InlineData(new byte[] { 0xfb, 0xff })]
    [InlineData(new byte[] { 0xfb, 0xff, 0xbf, 0x00 })]
    public void Base64Url_round_trips_without_padding(byte[] data)
    {
        var encoded = Base64Url.Encode(data);
        Assert.DoesNotContain('=', encoded);
        Assert.DoesNotContain('+', encoded);
        Assert.DoesNotContain('/', encoded);
        Assert.Equal(data, Base64Url.Decode(encoded));
    }

    [Fact]
    public void Random_values_are_url_safe_and_unique()
    {
        var values = Enumerable.Range(0, 100).Select(_ => Pkce.CreateRandomValue()).ToHashSet();
        Assert.Equal(100, values.Count);
        Assert.All(values, v => Assert.Matches("^[A-Za-z0-9_-]{22}$", v));
    }
}
