using System.Formats.Cbor;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Qrbound.Payments.Models;
using QRBusinessExchange.API.Services;
using QRBusinessExchange.Core.DTOs;
using QRBusinessExchange.Core.Entities;
using QRBusinessExchange.Infrastructure.Data;
using QRBusinessExchange.Infrastructure.Repositories;
using QRBusinessExchange.Infrastructure.Services;

namespace QRBusinessExchange.UnitTests;

/// <summary>
/// Builds the payment services the way the API does, over one in-memory database (never the shared
/// database), with a fake CardPointe, a fixed clock and the real WebAuthnService.
/// </summary>
internal sealed class PaymentTestRig
{
    private readonly string _dbName = Guid.NewGuid().ToString();

    // Singleton in the API too: passkey challenges must outlive a single request.
    private readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());

    private readonly IConfiguration _config = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["WebAuthn:RpId"] = "localhost",
            ["WebAuthn:Origins:0"] = "https://localhost:7090"
        })
        .Build();

    public FakeCardPointe Gateway { get; } = new();

    public FakeClock Clock { get; } = new(new DateTimeOffset(2026, 11, 25, 12, 0, 0, TimeSpan.Zero));

    public ApplicationDbContext NewDb() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(_dbName).Options);

    public SavedCardService SavedCards() => SavedCards(NewDb());

    // Each call gets its own DbContext, like separate HTTP requests.
    public QrPaymentService Payments()
    {
        var db = NewDb();
        return new QrPaymentService(
            db, Gateway, SavedCards(db), WebAuthn(db),
            Options.Create(new CardPointeOptions { Currency = "USD", MinAmountCents = 100, MaxAmountCents = 1_000_000 }),
            Clock, NullLogger<QrPaymentService>.Instance);
    }

    private SavedCardService SavedCards(ApplicationDbContext db) =>
        new(db, Gateway, Clock, NullLogger<SavedCardService>.Instance);

    private WebAuthnService WebAuthn(ApplicationDbContext db) =>
        new(new PasskeyRepository(db), new UserRepository(db), _config, _cache);
}

/// <summary>Records charges and cards on file instead of calling CardPointe.</summary>
internal sealed class FakeCardPointe : ICardPointeService
{
    public List<CardPointeChargeRequest> Charges { get; } = new();
    public List<string> DeletedProfiles { get; } = new();
    public Func<CardPointeResponse>? Next { get; set; }
    public Func<Task>? During { get; set; }
    private int _profiles;

    public async Task<CardPointeResponse> ChargeAsync(CardPointeChargeRequest request, CancellationToken cancellationToken = default)
    {
        Charges.Add(request);
        if (During is { } during)
        {
            During = null;
            await during();
        }

        return Next?.Invoke() ?? new CardPointeResponse
        {
            IsSuccess = true, StatusMessage = "Approved", TransactionId = "RETREF1", AuthorizationCode = "PPS123"
        };
    }

    public Task<CardPointeProfileResult> CreateProfileAsync(CardPointeProfileRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(new CardPointeProfileResult
        {
            IsSuccess = true,
            ProfileId = $"PROFILE{++_profiles}",
            AccountId = "1",
            Last4 = request.Token[^4..],
            CardBrand = "VISA"
        });

    public Task<bool> DeleteProfileAsync(string profileId, string accountId, CancellationToken cancellationToken = default)
    {
        DeletedProfiles.Add($"{profileId}/{accountId}");
        return Task.FromResult(true);
    }

    public PaymentConfig GetPublicConfig() => new();
}

internal sealed class FakeClock(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;
    public void Advance(TimeSpan by) => _now += by;
    public override DateTimeOffset GetUtcNow() => _now;
}

/// <summary>
/// A passkey (ES256, P-256) held by a test "phone": registers its public key for a user and signs
/// assertions the way a browser would after a fingerprint check.
/// </summary>
internal sealed class TestPasskey : IDisposable
{
    private readonly ECDsa _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private readonly byte[] _credentialId = RandomNumberGenerator.GetBytes(16);
    private uint _counter;

    public PasskeyCredential CredentialFor(Guid userId)
    {
        var p = _key.ExportParameters(false);
        var cose = new CborWriter();
        cose.WriteStartMap(5);
        cose.WriteInt32(1); cose.WriteInt32(2);      // kty: EC2
        cose.WriteInt32(3); cose.WriteInt32(-7);     // alg: ES256
        cose.WriteInt32(-1); cose.WriteInt32(1);     // crv: P-256
        cose.WriteInt32(-2); cose.WriteByteString(p.Q.X!);
        cose.WriteInt32(-3); cose.WriteByteString(p.Q.Y!);
        cose.WriteEndMap();

        return new PasskeyCredential
        {
            CredentialId = _credentialId,
            UserId = userId,
            PublicKey = cose.Encode(),
            Counter = 0,
            DeviceName = "Test phone"
        };
    }

    /// <summary>Signs the server's challenge, with user verification (the fingerprint) done.</summary>
    public AssertionResponseDto Sign(PasskeyAuthenticationOptionsDto options, string origin = "https://localhost:7090")
    {
        var rawChallenge = options.Challenge.Split(':')[1];
        var clientData = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            type = "webauthn.get",
            challenge = rawChallenge,
            origin
        }));

        _counter++;
        var authData = new byte[37];
        SHA256.HashData(Encoding.UTF8.GetBytes(options.RpId)).CopyTo(authData, 0);
        authData[32] = 0x01 | 0x04; // user present + user verified
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(authData.AsSpan(33), _counter);

        var signature = _key.SignData(authData.Concat(SHA256.HashData(clientData)).ToArray(),
            HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);

        return new AssertionResponseDto
        {
            Id = B64(_credentialId),
            RawId = B64(_credentialId),
            Type = "public-key",
            Response = new AssertionDataDto
            {
                ClientDataJSON = B64(clientData),
                AuthenticatorData = B64(authData),
                Signature = B64(signature)
            }
        };
    }

    private static string B64(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public void Dispose() => _key.Dispose();
}
