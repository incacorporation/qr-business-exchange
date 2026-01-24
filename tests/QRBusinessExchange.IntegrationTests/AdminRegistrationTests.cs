using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using QRBusinessExchange.API.DTOs;
using Xunit;

namespace QRBusinessExchange.IntegrationTests;

/// <summary>
/// Integration tests for Admin Registration endpoint
/// </summary>
public class AdminRegistrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public AdminRegistrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task RegisterAdmin_WithValidSecretAndData_ReturnsSuccess()
    {
        // Arrange
        var adminRequest = new RegisterRequest
        {
            Email = $"admin-{Guid.NewGuid()}@test.com", // Unique email
            Password = "Admin@123456",
            FirstName = "Admin",
            LastName = "User",
            CompanyName = "Test Company",
            PhoneNumber = "+1234567890"
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/Auth/register-admin")
        {
            Content = JsonContent.Create(adminRequest)
        };
        request.Headers.Add("X-Admin-Secret", "QRBizExchange2025!Admin");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(authResponse);
        Assert.NotNull(authResponse.AccessToken);
        Assert.NotNull(authResponse.RefreshToken);
        Assert.Equal(adminRequest.Email, authResponse.User.Email);
        Assert.Equal("Admin", authResponse.User.Role); // Verify role is Admin
        Assert.True(authResponse.User.IsEmailVerified); // Verify auto-verified
    }

    [Fact]
    public async Task RegisterAdmin_WithoutSecret_ReturnsForbidden()
    {
        // Arrange
        var adminRequest = new RegisterRequest
        {
            Email = $"admin-{Guid.NewGuid()}@test.com",
            Password = "Admin@123456",
            FirstName = "Admin",
            LastName = "User"
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/Auth/register-admin")
        {
            Content = JsonContent.Create(adminRequest)
        };
        // No X-Admin-Secret header

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task RegisterAdmin_WithInvalidSecret_ReturnsForbidden()
    {
        // Arrange
        var adminRequest = new RegisterRequest
        {
            Email = $"admin-{Guid.NewGuid()}@test.com",
            Password = "Admin@123456",
            FirstName = "Admin",
            LastName = "User"
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/Auth/register-admin")
        {
            Content = JsonContent.Create(adminRequest)
        };
        request.Headers.Add("X-Admin-Secret", "WrongSecret123");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task RegisterAdmin_WithDuplicateEmail_ReturnsBadRequest()
    {
        // Arrange - First registration
        var email = $"admin-duplicate-{Guid.NewGuid()}@test.com";
        var adminRequest = new RegisterRequest
        {
            Email = email,
            Password = "Admin@123456",
            FirstName = "Admin",
            LastName = "User"
        };

        var request1 = new HttpRequestMessage(HttpMethod.Post, "/api/Auth/register-admin")
        {
            Content = JsonContent.Create(adminRequest)
        };
        request1.Headers.Add("X-Admin-Secret", "QRBizExchange2025!Admin");

        await _client.SendAsync(request1); // First registration

        // Act - Second registration with same email
        var request2 = new HttpRequestMessage(HttpMethod.Post, "/api/Auth/register-admin")
        {
            Content = JsonContent.Create(adminRequest)
        };
        request2.Headers.Add("X-Admin-Secret", "QRBizExchange2025!Admin");

        var response = await _client.SendAsync(request2);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RegisterAdmin_ThenLogin_IncludesAdminRole()
    {
        // Arrange - Register admin
        var email = $"admin-login-{Guid.NewGuid()}@test.com";
        var password = "Admin@123456";
        var adminRequest = new RegisterRequest
        {
            Email = email,
            Password = password,
            FirstName = "Admin",
            LastName = "User"
        };

        var registerRequest = new HttpRequestMessage(HttpMethod.Post, "/api/Auth/register-admin")
        {
            Content = JsonContent.Create(adminRequest)
        };
        registerRequest.Headers.Add("X-Admin-Secret", "QRBizExchange2025!Admin");

        await _client.SendAsync(registerRequest);

        // Act - Login with admin credentials
        var loginRequest = new LoginRequest
        {
            Email = email,
            Password = password
        };

        var loginResponse = await _client.PostAsJsonAsync("/api/Auth/login", loginRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var authResponse = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(authResponse);
        Assert.Equal("Admin", authResponse.User.Role);
        Assert.NotNull(authResponse.AccessToken);
    }
}
