using Microsoft.AspNetCore.DataProtection;
using QRBusinessExchange.API.Services;
using QRBusinessExchange.Core.Entities;
using Xunit;

namespace QRBusinessExchange.UnitTests;

public class PasswordResetTokensTests
{
    private readonly PasswordResetTokens _tokens = new(new EphemeralDataProtectionProvider());

    private static User NewUser() => new()
    {
        UserId = Guid.NewGuid(),
        Email = "reset-test@example.test",
        PasswordHash = BCrypt.Net.BCrypt.HashPassword("Original-Pass-1"),
        IsActive = true
    };

    [Fact]
    public void Valid_token_identifies_the_user()
    {
        var user = NewUser();
        var token = _tokens.Create(user, DateTimeOffset.UtcNow);

        var userId = _tokens.ReadUserId(token, out var fingerprint);

        Assert.Equal(user.UserId, userId);
        Assert.True(PasswordResetTokens.MatchesCurrentPassword(user, fingerprint));
    }

    [Fact]
    public void Token_stops_working_once_the_password_changes()
    {
        var user = NewUser();
        var token = _tokens.Create(user, DateTimeOffset.UtcNow);

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword("New-Pass-2");

        _tokens.ReadUserId(token, out var fingerprint);
        Assert.False(PasswordResetTokens.MatchesCurrentPassword(user, fingerprint));
    }

    [Fact]
    public void Token_stops_working_if_the_email_changes_or_account_is_deactivated()
    {
        var user = NewUser();
        var token = _tokens.Create(user, DateTimeOffset.UtcNow);
        _tokens.ReadUserId(token, out var fingerprint);

        user.Email = "someone-else@example.test";
        Assert.False(PasswordResetTokens.MatchesCurrentPassword(user, fingerprint));

        user.Email = "reset-test@example.test";
        user.IsActive = false;
        Assert.False(PasswordResetTokens.MatchesCurrentPassword(user, fingerprint));
    }

    [Fact]
    public void Expired_token_is_rejected()
    {
        var user = NewUser();
        var issuedLongAgo = DateTimeOffset.UtcNow - PasswordResetTokens.Lifetime - TimeSpan.FromMinutes(1);
        var token = _tokens.Create(user, issuedLongAgo);

        Assert.Null(_tokens.ReadUserId(token, out _));
    }

    [Fact]
    public void Tampered_token_is_rejected()
    {
        var token = _tokens.Create(NewUser(), DateTimeOffset.UtcNow);
        var middle = token.Length / 2;
        var tampered = token[..middle] + (token[middle] == 'A' ? 'B' : 'A') + token[(middle + 1)..];

        Assert.Null(_tokens.ReadUserId(tampered, out _));
    }

    [Fact]
    public void Token_from_other_keys_is_rejected()
    {
        var otherServer = new PasswordResetTokens(new EphemeralDataProtectionProvider());
        var token = otherServer.Create(NewUser(), DateTimeOffset.UtcNow);

        Assert.Null(_tokens.ReadUserId(token, out _));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-token")]
    public void Garbage_is_rejected(string? token)
    {
        Assert.Null(_tokens.ReadUserId(token, out var fingerprint));
        Assert.Null(fingerprint);
    }

    [Fact]
    public void Missing_fingerprint_never_matches()
    {
        Assert.False(PasswordResetTokens.MatchesCurrentPassword(NewUser(), null));
    }
}
