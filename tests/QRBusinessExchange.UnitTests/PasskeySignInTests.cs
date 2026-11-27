using System.Reflection;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using QRBusinessExchange.Core.DTOs;
using QRBusinessExchange.Core.Entities;
using QRBusinessExchange.Core.Interfaces;
using QRBusinessExchange.Infrastructure.Services;

namespace QRBusinessExchange.UnitTests;

public class PasskeySignInTests
{
    private static WebAuthnService CreateService(out MemoryCache cache)
    {
        cache = new MemoryCache(new MemoryCacheOptions());
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WebAuthn:RpId"] = "www.qrbound.com",
                ["WebAuthn:Origins:0"] = "https://www.qrbound.com"
            })
            .Build();

        return new WebAuthnService(
            RepositoryStub<IPasskeyRepository>.Create(),
            RepositoryStub<IUserRepository>.Create(),
            config,
            cache);
    }

    private static AssertionResponseDto AssertionFor(string rawId) => new()
    {
        Id = rawId,
        RawId = rawId,
        Type = "public-key",
        Response = new AssertionDataDto { ClientDataJSON = "e30", AuthenticatorData = "AA", Signature = "AA" }
    };

    [Fact]
    public async Task Unknown_passkey_is_reported_as_not_registered()
    {
        var service = CreateService(out _);
        var options = await service.GenerateAuthenticationOptionsAsync();

        await Assert.ThrowsAsync<PasskeyNotRegisteredException>(() =>
            service.CompleteAuthenticationAsync(options.Challenge, AssertionFor("dW5rbm93bi1jcmVkZW50aWFs")));
    }

    [Fact]
    public async Task Challenge_is_used_up_even_when_the_passkey_is_unknown()
    {
        var service = CreateService(out _);
        var options = await service.GenerateAuthenticationOptionsAsync();

        await Assert.ThrowsAsync<PasskeyNotRegisteredException>(() =>
            service.CompleteAuthenticationAsync(options.Challenge, AssertionFor("dW5rbm93bg")));

        // A second attempt with the same challenge must not get as far as the passkey lookup.
        var replay = await Assert.ThrowsAnyAsync<InvalidOperationException>(() =>
            service.CompleteAuthenticationAsync(options.Challenge, AssertionFor("dW5rbm93bg")));
        Assert.IsNotType<PasskeyNotRegisteredException>(replay);
    }

    [Fact]
    public async Task Options_carry_the_key_and_challenge_the_browser_script_splits()
    {
        var service = CreateService(out _);
        var options = await service.GenerateAuthenticationOptionsAsync();

        // webauthn-interop.js signs only the part after the colon; the server needs the key before it.
        var parts = options.Challenge.Split(':');
        Assert.Equal(2, parts.Length);
        Assert.Matches("^[0-9a-f]{32}$", parts[0]);
        Assert.Matches("^[A-Za-z0-9_-]{43}$", parts[1]);
        Assert.Equal("www.qrbound.com", options.RpId);
    }

    /// <summary>
    /// Minimal repository stand-in: lookups find nothing, anything else isn't expected in these tests.
    /// </summary>
    public class RepositoryStub<T> : DispatchProxy where T : class
    {
        public static T Create() => DispatchProxy.Create<T, RepositoryStub<T>>();

        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method?.Name == nameof(IPasskeyRepository.GetByCredentialIdAsync))
            {
                return Task.FromResult<PasskeyCredential?>(null);
            }

            throw new NotSupportedException($"{typeof(T).Name}.{method?.Name} isn't used by these tests.");
        }
    }
}
