using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace QRBusinessExchange.IntegrationTests;

/// <summary>
/// Integration tests for Email functionality
/// </summary>
public class EmailServiceTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public EmailServiceTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task SmtpStatus_ReturnsConfigurationStatus()
    {
        // Act
        var response = await _client.GetAsync("/api/EmailTest/smtp-status");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<SmtpStatusResponse>();
        Assert.NotNull(result);
        
        // Configuration might be valid or invalid depending on setup
        // Just verify the endpoint responds
        Assert.NotNull(result.Message);
    }

    [Fact(Skip = "Requires SMTP configuration and sends real email")]
    public async Task SendTestEmail_WithValidEmail_SendsEmail()
    {
        // Arrange
        var testEmail = "test@example.com"; // Change to your test email

        // Act
        var response = await _client.PostAsync(
            $"/api/EmailTest/send-test-email?email={testEmail}", 
            null);

        // Assert
        // This will fail if SMTP is not configured
        // Uncomment and configure SMTP to run this test
        var content = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"Response: {content}");
    }

    [Fact]
    public async Task SendTestEmail_WithoutEmail_ReturnsBadRequest()
    {
        // Act
        var response = await _client.PostAsync("/api/EmailTest/send-test-email", null);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SendTestEmail_WithInvalidEmail_ReturnsBadRequest()
    {
        // Act
        var response = await _client.PostAsync(
            "/api/EmailTest/send-test-email?email=invalid-email", 
            null);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // Helper class for deserialization
    private class SmtpStatusResponse
    {
        public bool Configured { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? Error { get; set; }
        public string? Hint { get; set; }
    }
}
