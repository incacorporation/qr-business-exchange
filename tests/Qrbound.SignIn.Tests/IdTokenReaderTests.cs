namespace Qrbound.SignIn.Tests;

public class IdTokenReaderTests
{
    private readonly TestTokens _tokens = new();
    private static readonly TimeSpan Skew = TimeSpan.FromMinutes(1);

    private static QrboundUser Read(string token, string? nonce = "n-1") =>
        QrboundIdTokenReader.Read(token, TestTokens.Issuer, TestTokens.ClientId, nonce, DateTimeOffset.UtcNow, Skew);

    [Fact]
    public void Reads_user_from_a_valid_id_token()
    {
        var user = Read(_tokens.Create());

        Assert.Equal("11111111-2222-3333-4444-555555555555", user.Subject);
        Assert.Equal("user@example.test", user.Email);
        Assert.True(user.EmailVerified);
        Assert.Equal("Test User", user.Name);
        Assert.Equal("qr", user.AuthMethod);
        Assert.Equal("Admin", user.BusinessRole);
        Assert.Equal("acme", user.BusinessSlug);
        Assert.Equal(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"), user.BusinessId);
        Assert.True(user.ExpiresAt > DateTimeOffset.UtcNow);
        Assert.Contains(user.Claims, c => c.Type == "email_verified" && c.Value == "true");

        var principal = user.ToPrincipal();
        Assert.True(principal.Identity!.IsAuthenticated);
        Assert.Equal("Test User", principal.Identity.Name);
        Assert.True(principal.IsInRole("Admin"));
    }

    [Fact]
    public void Issuer_with_trailing_slash_is_equivalent()
    {
        var user = QrboundIdTokenReader.Read(_tokens.Create(), TestTokens.Issuer + "/", TestTokens.ClientId, "n-1",
            DateTimeOffset.UtcNow, Skew);
        Assert.Equal("user@example.test", user.Email);
    }

    [Fact]
    public void Wrong_issuer_is_rejected() =>
        Assert.Throws<QrboundTokenException>(() => Read(_tokens.Create(issuer: "https://evil.test")));

    [Fact]
    public void Token_for_another_app_is_rejected() =>
        Assert.Throws<QrboundTokenException>(() => Read(_tokens.Create(audience: "other_app")));

    [Fact]
    public void Expired_token_is_rejected() =>
        Assert.Throws<QrboundTokenException>(() => Read(_tokens.Create(expires: DateTime.UtcNow.AddMinutes(-5))));

    [Fact]
    public void Nonce_mismatch_is_rejected() =>
        Assert.Throws<QrboundTokenException>(() => Read(_tokens.Create(nonce: "someone-else")));

    [Fact]
    public void Missing_nonce_is_rejected_when_one_was_sent() =>
        Assert.Throws<QrboundTokenException>(() => Read(_tokens.Create(nonce: null)));

    [Fact]
    public void Access_token_is_not_accepted_as_id_token() =>
        Assert.Throws<QrboundTokenException>(() => Read(_tokens.Create(tokenUse: "access")));

    [Theory]
    [InlineData("")]
    [InlineData("not-a-jwt")]
    [InlineData("a.b")]
    [InlineData("a.%%%.c")]
    public void Malformed_tokens_are_rejected(string token) =>
        Assert.Throws<QrboundTokenException>(() => Read(token));
}
