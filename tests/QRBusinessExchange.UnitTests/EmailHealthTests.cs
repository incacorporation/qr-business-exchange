using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using QRBusinessExchange.API.Controllers;
using QRBusinessExchange.Core.Entities;
using QRBusinessExchange.Core.Interfaces;
using QRBusinessExchange.Infrastructure.Configuration;

namespace QRBusinessExchange.UnitTests;

/// <summary>The email setup check and the admin-only test email (for diagnosing mail delivery).</summary>
public class EmailHealthTests
{
    private readonly Dictionary<Guid, User> users = new();
    private readonly FakeEmail email = new();

    private HealthController Controller(EmailSettings settings, User? caller = null)
    {
        var http = new DefaultHttpContext();
        if (caller != null)
        {
            users[caller.UserId] = caller;
            http.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", caller.UserId.ToString())], "test"));
        }

        return new HealthController(null!, NullLogger<HealthController>.Instance, Options.Create(settings), email, new FakeUsers(users))
        {
            ControllerContext = new ControllerContext { HttpContext = http }
        };
    }

    private static EmailSettings Settings(string password) => new()
    {
        SmtpHost = "qrbound.com", SmtpPort = 587, SmtpUser = "noreply@qrbound.com", SmtpPassword = password,
        FromEmail = "noreply@qrbound.com", FromName = "qrbound"
    };

    private static User NewUser(string role) => new()
    {
        UserId = Guid.NewGuid(), Email = "admin@example.com", FirstName = "A", LastName = "B", Role = role
    };

    private static T Prop<T>(object value, string name) => (T)value.GetType().GetProperty(name)!.GetValue(value)!;

    [Fact]
    public void Setup_check_reports_a_missing_password_without_showing_one()
    {
        var missing = Assert.IsType<OkObjectResult>(Controller(Settings("")).GetEmailSetup()).Value!;
        Assert.Equal("incomplete", Prop<string>(missing, "status"));
        Assert.False(Prop<bool>(missing, "passwordSet"));

        var set = Assert.IsType<OkObjectResult>(Controller(Settings("not-a-real-password")).GetEmailSetup()).Value!;
        Assert.Equal("configured", Prop<string>(set, "status"));
        Assert.True(Prop<bool>(set, "passwordSet"));
        Assert.DoesNotContain(set.GetType().GetProperties(), p => p.GetValue(set) as string == "not-a-real-password");
    }

    [Fact]
    public async Task Ordinary_user_cannot_send_test_emails()
    {
        var result = await Controller(Settings("x"), NewUser("User")).SendTestEmail(new() { To = "someone@example.com" });

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<ObjectResult>(result).StatusCode);
        Assert.Empty(email.Sent);
    }

    [Fact]
    public async Task Admin_gets_the_mail_server_error_back()
    {
        email.NextError = "Failure sending mail. → Authentication failed";

        var result = await Controller(Settings("x"), NewUser("SuperAdmin")).SendTestEmail(new() { To = "someone@example.com" });

        var body = Assert.IsType<OkObjectResult>(result).Value!;
        Assert.False(Prop<bool>(body, "sent"));
        Assert.Equal("Failure sending mail. → Authentication failed", Prop<string>(body, "error"));
        Assert.Equal(["someone@example.com"], email.Sent);
    }

    private sealed class FakeEmail : IEmailService
    {
        public List<string> Sent { get; } = new();
        public string? NextError { get; set; }
        public Task<string?> SendTestEmailAsync(string to) { Sent.Add(to); return Task.FromResult(NextError); }
        public Task<bool> SendVerificationEmailAsync(Guid userId, string email, string verificationToken) => Task.FromResult(true);
        public Task<bool> SendConnectionNotificationAsync(Guid recipientUserId, string senderName, string? message = null) => Task.FromResult(true);
        public Task<bool> SendPasswordResetEmailAsync(string email, string firstName, string resetUrl, TimeSpan validFor) => Task.FromResult(true);
        public Task<bool> SendPasswordChangedEmailAsync(string email, string firstName, string forgotPasswordUrl) => Task.FromResult(true);
    }

    private sealed class FakeUsers(Dictionary<Guid, User> items) : IUserRepository
    {
        public Task<User?> GetByIdAsync(Guid userId) => Task.FromResult(items.GetValueOrDefault(userId));
        public Task<User?> GetByEmailAsync(string email) => Task.FromResult(items.Values.FirstOrDefault(u => u.Email == email));
        public Task<User> CreateAsync(User user) => Task.FromResult(user);
        public Task<User> UpdateAsync(User user) => Task.FromResult(user);
        public Task<bool> DeleteAsync(Guid userId) => Task.FromResult(items.Remove(userId));
    }
}
