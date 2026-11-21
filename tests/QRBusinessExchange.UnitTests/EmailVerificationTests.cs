using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using QRBusinessExchange.API.Controllers;
using QRBusinessExchange.API.Services;
using QRBusinessExchange.Core.DTOs;
using QRBusinessExchange.Core.Entities;
using QRBusinessExchange.Core.Interfaces;
using QRBusinessExchange.Infrastructure.Configuration;
using Xunit;

namespace QRBusinessExchange.UnitTests;

public class EmailVerificationTokensTests
{
    private readonly EmailVerificationTokens _tokens = new(new EphemeralDataProtectionProvider());

    private static User NewUser() => new()
    {
        UserId = Guid.NewGuid(),
        Email = "verify-test@example.test",
        PasswordHash = "hash",
        IsActive = true
    };

    [Fact]
    public void Valid_token_identifies_the_user_and_address()
    {
        var user = NewUser();
        var token = _tokens.Create(user, DateTimeOffset.UtcNow);

        var userId = _tokens.ReadUserId(token, out var fingerprint);

        Assert.Equal(user.UserId, userId);
        Assert.True(EmailVerificationTokens.MatchesCurrentEmail(user, fingerprint));
    }

    [Fact]
    public void Address_match_ignores_case()
    {
        var user = NewUser();
        var token = _tokens.Create(user, DateTimeOffset.UtcNow);
        _tokens.ReadUserId(token, out var fingerprint);

        user.Email = "Verify-Test@Example.TEST";
        Assert.True(EmailVerificationTokens.MatchesCurrentEmail(user, fingerprint));
    }

    [Fact]
    public void Token_stops_working_if_the_email_changes()
    {
        var user = NewUser();
        var token = _tokens.Create(user, DateTimeOffset.UtcNow);
        _tokens.ReadUserId(token, out var fingerprint);

        user.Email = "someone-else@example.test";
        Assert.False(EmailVerificationTokens.MatchesCurrentEmail(user, fingerprint));
    }

    [Fact]
    public void Expired_token_is_rejected()
    {
        var issuedLongAgo = DateTimeOffset.UtcNow - EmailVerificationTokens.Lifetime - TimeSpan.FromMinutes(1);
        var token = _tokens.Create(NewUser(), issuedLongAgo);

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
    public void Password_reset_token_cannot_be_used_to_verify()
    {
        var provider = new EphemeralDataProtectionProvider();
        var resetToken = new PasswordResetTokens(provider).Create(NewUser(), DateTimeOffset.UtcNow);

        Assert.Null(new EmailVerificationTokens(provider).ReadUserId(resetToken, out _));
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
}

public class EmailVerificationEndpointTests
{
    private readonly FakeUsers _users = new();
    private readonly RecordingEmail _email = new();
    private readonly EmailVerificationTokens _tokens = new(new EphemeralDataProtectionProvider());
    private readonly AuthController _controller;

    public EmailVerificationEndpointTests()
    {
        var services = new ServiceCollection()
            .AddSingleton<IUserRepository>(_users)
            .AddSingleton<IEmailService>(_email)
            .BuildServiceProvider();

        _controller = new AuthController(
            _users,
            jwtService: null!, // not used by these endpoints
            _email,
            NullLogger<AuthController>.Instance,
            new PasswordResetTokens(new EphemeralDataProtectionProvider()),
            _tokens,
            new MemoryCache(new MemoryCacheOptions()),
            services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new SsoSettings { PortalBaseUrl = "https://portal.example.test" }),
            new TestEnvironment(),
            new ConfigurationBuilder().Build())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    private User AddUser(bool verified = false, bool active = true)
    {
        var user = new User
        {
            UserId = Guid.NewGuid(),
            Email = $"user-{Guid.NewGuid():N}@example.test",
            PasswordHash = "hash",
            IsActive = active,
            IsEmailVerified = verified
        };
        _users.Items[user.UserId] = user;
        return user;
    }

    private void SignIn(User user) =>
        _controller.ControllerContext.HttpContext.User =
            new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", user.UserId.ToString()) }, "Bearer"));

    [Fact]
    public async Task Valid_link_marks_the_email_verified()
    {
        var user = AddUser();
        var token = _tokens.Create(user, DateTimeOffset.UtcNow);

        var result = await _controller.VerifyEmail(new VerifyEmailRequest { Token = token });

        Assert.IsType<OkObjectResult>(result);
        Assert.True(_users.Items[user.UserId].IsEmailVerified);
    }

    [Fact]
    public async Task Using_the_link_again_is_harmless()
    {
        var user = AddUser();
        var token = _tokens.Create(user, DateTimeOffset.UtcNow);

        await _controller.VerifyEmail(new VerifyEmailRequest { Token = token });
        var again = await _controller.VerifyEmail(new VerifyEmailRequest { Token = token });

        Assert.IsType<OkObjectResult>(again);
        Assert.True(_users.Items[user.UserId].IsEmailVerified);
    }

    [Fact]
    public async Task Bad_token_is_rejected_and_nothing_changes()
    {
        var user = AddUser();

        var result = await _controller.VerifyEmail(new VerifyEmailRequest { Token = "not-a-token" });

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.False(user.IsEmailVerified);
    }

    [Fact]
    public async Task Link_for_an_old_address_is_rejected()
    {
        var user = AddUser();
        var token = _tokens.Create(user, DateTimeOffset.UtcNow);
        user.Email = "changed@example.test";

        var result = await _controller.VerifyEmail(new VerifyEmailRequest { Token = token });

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.False(user.IsEmailVerified);
    }

    [Fact]
    public async Task Deactivated_account_is_rejected()
    {
        var user = AddUser(active: false);
        var token = _tokens.Create(user, DateTimeOffset.UtcNow);

        var result = await _controller.VerifyEmail(new VerifyEmailRequest { Token = token });

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.False(user.IsEmailVerified);
    }

    [Fact]
    public async Task Resend_by_email_sends_a_link_that_verifies()
    {
        var user = AddUser();

        var result = await _controller.ResendVerification(new ResendVerificationRequest { Email = user.Email });

        Assert.IsType<AcceptedResult>(result);
        var token = await _email.WaitForTokenAsync(user.UserId);
        Assert.IsType<OkObjectResult>(await _controller.VerifyEmail(new VerifyEmailRequest { Token = token }));
        Assert.True(user.IsEmailVerified);
    }

    [Fact]
    public async Task Resend_for_signed_in_user_needs_no_email()
    {
        var user = AddUser();
        SignIn(user);

        var result = await _controller.ResendVerification(new ResendVerificationRequest());

        Assert.IsType<AcceptedResult>(result);
        Assert.NotNull(await _email.WaitForTokenAsync(user.UserId));
    }

    [Fact]
    public async Task Resend_answers_the_same_for_unknown_and_verified_accounts_and_sends_nothing()
    {
        var verified = AddUser(verified: true);

        var unknown = await _controller.ResendVerification(new ResendVerificationRequest { Email = "nobody@example.test" });
        var already = await _controller.ResendVerification(new ResendVerificationRequest { Email = verified.Email });

        Assert.IsType<AcceptedResult>(unknown);
        Assert.IsType<AcceptedResult>(already);
        await Task.Delay(200);
        Assert.Empty(_email.Sent);
    }

    [Fact]
    public async Task Resend_is_throttled_per_account()
    {
        var user = AddUser();

        await _controller.ResendVerification(new ResendVerificationRequest { Email = user.Email });
        await _email.WaitForTokenAsync(user.UserId);
        await _controller.ResendVerification(new ResendVerificationRequest { Email = user.Email });
        await Task.Delay(200);

        Assert.Single(_email.Sent);
    }

    [Fact]
    public async Task Resend_without_session_or_email_is_a_bad_request()
    {
        var result = await _controller.ResendVerification(new ResendVerificationRequest());

        Assert.IsType<BadRequestObjectResult>(result);
    }

    private sealed class FakeUsers : IUserRepository
    {
        public ConcurrentDictionary<Guid, User> Items { get; } = new();
        public Task<User?> GetByIdAsync(Guid userId) => Task.FromResult(Items.GetValueOrDefault(userId));
        public Task<User?> GetByEmailAsync(string email) =>
            Task.FromResult(Items.Values.FirstOrDefault(u => string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase)));
        public Task<User> CreateAsync(User user) { Items[user.UserId] = user; return Task.FromResult(user); }
        public Task<User> UpdateAsync(User user) { Items[user.UserId] = user; return Task.FromResult(user); }
        public Task<bool> DeleteAsync(Guid userId) => Task.FromResult(Items.TryRemove(userId, out _));
    }

    private sealed class RecordingEmail : IEmailService
    {
        public ConcurrentQueue<(Guid UserId, string Token)> Sent { get; } = new();

        public Task<bool> SendVerificationEmailAsync(Guid userId, string email, string verificationToken)
        {
            Sent.Enqueue((userId, verificationToken));
            return Task.FromResult(true);
        }

        public async Task<string> WaitForTokenAsync(Guid userId)
        {
            for (var i = 0; i < 100; i++)
            {
                var match = Sent.FirstOrDefault(s => s.UserId == userId);
                if (match.Token != null) return match.Token;
                await Task.Delay(20);
            }
            throw new TimeoutException("No verification email was sent.");
        }

        public Task<bool> SendConnectionNotificationAsync(Guid recipientUserId, string senderName, string? message = null) => Task.FromResult(true);
        public Task<bool> SendPasswordResetEmailAsync(string email, string firstName, string resetUrl, TimeSpan validFor) => Task.FromResult(true);
        public Task<bool> SendPasswordChangedEmailAsync(string email, string firstName, string forgotPasswordUrl) => Task.FromResult(true);
    }

    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "QRBusinessExchange.API";
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
