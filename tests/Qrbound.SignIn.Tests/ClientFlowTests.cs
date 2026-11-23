using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Qrbound.SignIn.Tests;

/// <summary>
/// Runs QrboundClient against a fake qrbound API and checks the PKCE binding end to end:
/// the challenge sent when collecting the code must match the verifier sent to the token endpoint.
/// </summary>
public class ClientFlowTests
{
    private readonly TestTokens _tokens = new();

    private static QrboundOptions Options() => new()
    {
        Authority = TestTokens.Issuer,
        PortalBaseUrl = "https://portal.test",
        ClientId = TestTokens.ClientId,
        RedirectUri = "https://app.test/qrbound/callback"
    };

    [Fact]
    public async Task Qr_flow_binds_code_to_pkce_and_returns_user()
    {
        var api = new FakeQrbound(_tokens);
        var client = new QrboundClient(new FakeFactory(api), Microsoft.Extensions.Options.Options.Create(Options()),
            new MemoryStateStore());

        var session = await client.StartQrSignInAsync();
        Assert.Equal("42", session.VerificationCode);
        Assert.Equal("https://portal.test/qr/approve/" + FakeQrbound.SessionId, session.ApproveUrl);
        Assert.Equal(TestTokens.ClientId, api.CreateBody!.RootElement.GetProperty("clientId").GetString());
        Assert.Equal("https://app.test/qrbound/callback", api.CreateBody.RootElement.GetProperty("redirectUri").GetString());

        Assert.Equal(QrboundStatus.Pending, (await client.PollAsync(session)).Status);

        api.Approved = true;
        var poll = await client.PollAsync(session);
        Assert.Equal(QrboundStatus.Approved, poll.Status);
        Assert.Equal("the-code", poll.Code);

        var result = await client.CompleteAsync(session, poll.Code!);
        Assert.Equal("user@example.test", result.User.Email);
        Assert.NotNull(result.Tokens);

        // The PKCE pair the API saw must match.
        Assert.Equal("S256", api.LastAuthorize!.RootElement.GetProperty("codeChallengeMethod").GetString());
        var challenge = api.LastAuthorize.RootElement.GetProperty("codeChallenge").GetString()!;
        Assert.True(Pkce.Verify(api.TokenForm!["code_verifier"], challenge));
        Assert.Equal("authorization_code", api.TokenForm["grant_type"]);
        Assert.False(api.TokenForm.ContainsKey("client_secret"));
        Assert.Equal("https://app.test/qrbound/callback", api.TokenForm["redirect_uri"]);
    }

    [Fact]
    public async Task Register_mode_marks_the_approve_url()
    {
        var api = new FakeQrbound(_tokens);
        var client = new QrboundClient(new FakeFactory(api), Microsoft.Extensions.Options.Options.Create(Options()), new MemoryStateStore());
        var session = await client.StartQrSignInAsync(QrboundMode.Register);
        Assert.EndsWith("?register=1", session.ApproveUrl);
    }

    [Fact]
    public async Task Approved_response_with_a_different_state_is_rejected()
    {
        var api = new FakeQrbound(_tokens) { Approved = true, OverrideState = "attacker" };
        var client = new QrboundClient(new FakeFactory(api), Microsoft.Extensions.Options.Options.Create(Options()), new MemoryStateStore());
        var session = await client.StartQrSignInAsync();
        var ex = await Assert.ThrowsAsync<QrboundException>(() => client.PollAsync(session));
        Assert.Equal("invalid_state", ex.Error);
    }

    [Fact]
    public async Task Id_token_with_wrong_nonce_fails_the_sign_in()
    {
        var api = new FakeQrbound(_tokens) { Approved = true, NonceOverride = "replayed" };
        var client = new QrboundClient(new FakeFactory(api), Microsoft.Extensions.Options.Options.Create(Options()), new MemoryStateStore());
        var session = await client.StartQrSignInAsync();
        var poll = await client.PollAsync(session);
        var ex = await Assert.ThrowsAsync<QrboundException>(() => client.CompleteAsync(session, poll.Code!));
        Assert.Equal("invalid_token", ex.Error);
    }

    [Fact]
    public async Task Category_is_sent_and_a_pending_approval_is_reported_with_its_message()
    {
        var api = new FakeQrbound(_tokens) { Approved = true, AccessOutcome = QrboundStatus.AccessPending };
        var client = new QrboundClient(new FakeFactory(api), Microsoft.Extensions.Options.Options.Create(Options()), new MemoryStateStore());

        var session = await client.StartQrSignInAsync(QrboundMode.Register, "business");
        Assert.Equal("business", session.Category);
        Assert.Equal("business", api.CreateBody!.RootElement.GetProperty("category").GetString());

        var poll = await client.PollAsync(session);
        Assert.Equal(QrboundStatus.AccessPending, poll.Status);
        Assert.Equal("Waiting for Eat in Houston", poll.Message);
        Assert.Null(poll.Code);
        Assert.True(QrboundStatus.IsFinal(poll.Status));
    }

    [Fact]
    public void Redirect_url_carries_the_category()
    {
        var url = QrboundClient.BuildAuthorizeUrl(Options(), "s", "n", new string('c', 43), category: "business");
        Assert.Contains("&category=business", url);
        Assert.DoesNotContain("category=", QrboundClient.BuildAuthorizeUrl(Options(), "s", "n", new string('c', 43)));
    }

    [Fact]
    public async Task Missing_session_is_reported_as_expired()
    {
        var api = new FakeQrbound(_tokens) { Gone = true };
        var client = new QrboundClient(new FakeFactory(api), Microsoft.Extensions.Options.Options.Create(Options()), new MemoryStateStore());
        var session = await client.StartQrSignInAsync();
        Assert.Equal(QrboundStatus.Expired, (await client.PollAsync(session)).Status);
    }

    [Fact]
    public async Task Server_without_the_authorize_endpoint_is_an_error_not_an_expiry()
    {
        var api = new FakeQrbound(_tokens) { EndpointMissing = true };
        var client = new QrboundClient(new FakeFactory(api), Microsoft.Extensions.Options.Options.Create(Options()), new MemoryStateStore());
        var session = await client.StartQrSignInAsync();
        var ex = await Assert.ThrowsAsync<QrboundException>(() => client.PollAsync(session));
        Assert.Equal("unsupported_server", ex.Error);
    }

    [Fact]
    public async Task Api_errors_surface_as_QrboundException()
    {
        var api = new FakeQrbound(_tokens) { CreateError = ("invalid_redirect_uri", "not registered") };
        var client = new QrboundClient(new FakeFactory(api), Microsoft.Extensions.Options.Options.Create(Options()), new MemoryStateStore());
        var ex = await Assert.ThrowsAsync<QrboundException>(() => client.StartQrSignInAsync());
        Assert.Equal("invalid_redirect_uri", ex.Error);
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task Redirect_flow_round_trips_state_and_verifier()
    {
        var api = new FakeQrbound(_tokens);
        var store = new MemoryStateStore();
        var client = new QrboundClient(new FakeFactory(api), Microsoft.Extensions.Options.Options.Create(Options()), store);

        var url = new Uri(await client.BuildAuthorizeUrlAsync("/account"));
        Assert.Equal("https://portal.test/sso/authorize", url.GetLeftPart(UriPartial.Path));
        var query = QrboundClient.ParseQuery(url.Query);
        Assert.Equal(TestTokens.ClientId, query["client_id"]);
        Assert.Equal("S256", query["code_challenge_method"]);
        Assert.Equal("code", query["response_type"]);

        api.NonceOverride = query["nonce"];
        var result = await client.HandleCallbackAsync(new Uri($"https://app.test/qrbound/callback?code=abc&state={query["state"]}"));
        Assert.Equal("/account", result.ReturnUrl);
        Assert.True(Pkce.Verify(api.TokenForm!["code_verifier"], query["code_challenge"]));

        // Single use: replaying the callback fails.
        var replay = await Assert.ThrowsAsync<QrboundException>(() =>
            client.HandleCallbackAsync(new Uri($"https://app.test/qrbound/callback?code=abc&state={query["state"]}")));
        Assert.Equal("invalid_state", replay.Error);
    }

    [Fact]
    public async Task Callback_with_unknown_state_is_rejected()
    {
        var client = new QrboundClient(new FakeFactory(new FakeQrbound(_tokens)), Microsoft.Extensions.Options.Options.Create(Options()), new MemoryStateStore());
        var ex = await Assert.ThrowsAsync<QrboundException>(() =>
            client.HandleCallbackAsync(new Uri("https://app.test/qrbound/callback?code=abc&state=forged")));
        Assert.Equal("invalid_state", ex.Error);
    }

    [Fact]
    public async Task Callback_error_is_reported()
    {
        var client = new QrboundClient(new FakeFactory(new FakeQrbound(_tokens)), Microsoft.Extensions.Options.Options.Create(Options()), new MemoryStateStore());
        var ex = await Assert.ThrowsAsync<QrboundException>(() =>
            client.HandleCallbackAsync(new Uri("https://app.test/qrbound/callback?error=access_denied&error_description=cancelled&state=x")));
        Assert.Equal("access_denied", ex.Error);
        Assert.Equal("cancelled", ex.Message);
    }

    [Theory]
    [InlineData("https://p.test/qr/approve/1", "https://p.test/qr/approve/1?register=1")]
    [InlineData("https://p.test/qr/approve/1?a=b", "https://p.test/qr/approve/1?a=b&register=1")]
    [InlineData("https://p.test/qr/link/1#k=secret", "https://p.test/qr/link/1?register=1#k=secret")]
    public void AppendQuery_keeps_fragment_last(string input, string expected) =>
        Assert.Equal(expected, QrboundClient.AppendQuery(input, "register", "1"));

    [Fact]
    public void Options_validation_requires_client_and_redirect()
    {
        Assert.Throws<InvalidOperationException>(() => new QrboundOptions { RedirectUri = "https://a.test/cb" }.Validate());
        Assert.Throws<InvalidOperationException>(() => new QrboundOptions { ClientId = "x", RedirectUri = "/relative" }.Validate());
        new QrboundOptions { ClientId = "x", RedirectUri = "https://a.test/cb" }.Validate();
    }

    [Fact]
    public void Qr_svg_renders_square_matrix()
    {
        var svg = QrSvg.Render("https://portal.test/qr/approve/123");
        Assert.StartsWith("<svg", svg);
        Assert.Contains("viewBox=\"0 0 ", svg);
        Assert.Contains("<path", svg);
    }

    // ------------------------------------------------------------------ fakes

    private sealed class MemoryStateStore : IQrboundStateStore
    {
        private readonly Dictionary<string, QrboundPendingRedirect> _items = new();
        public ValueTask SaveAsync(QrboundPendingRedirect pending) { _items[pending.State] = pending; return ValueTask.CompletedTask; }
        public ValueTask<QrboundPendingRedirect?> TakeAsync(string state) =>
            ValueTask.FromResult(_items.Remove(state, out var p) ? p : null);
    }

    private sealed class FakeFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, false) { BaseAddress = new Uri(TestTokens.Issuer + "/") };
    }

    private sealed class FakeQrbound(TestTokens tokens) : HttpMessageHandler
    {
        public static readonly Guid SessionId = Guid.Parse("00000000-0000-0000-0000-0000000000aa");

        public bool Approved { get; set; }
        public bool Gone { get; set; }
        public bool EndpointMissing { get; set; }
        public string? AccessOutcome { get; set; } // "AccessPending" or "AccessDenied" once approved
        public string? OverrideState { get; set; }
        public string? NonceOverride { get; set; }
        public (string Error, string Description)? CreateError { get; set; }

        public JsonDocument? CreateBody { get; private set; }
        public JsonDocument? LastAuthorize { get; private set; }
        public Dictionary<string, string>? TokenForm { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            var body = request.Content == null ? "" : await request.Content.ReadAsStringAsync(ct);

            if (path == "/api/qr-login/sessions")
            {
                CreateBody = JsonDocument.Parse(body);
                if (CreateError is { } e) return Json(HttpStatusCode.BadRequest, new { error = e.Error, error_description = e.Description });
                return Json(HttpStatusCode.OK, new
                {
                    sessionId = SessionId,
                    pollSecret = "poll-secret",
                    approveUrl = $"https://portal.test/qr/approve/{SessionId}",
                    verificationCode = "42",
                    expiresAt = DateTime.UtcNow.AddMinutes(3),
                    pollIntervalSeconds = 2
                });
            }

            if (path == $"/api/qr-login/sessions/{SessionId}/authorize")
            {
                if (EndpointMissing) return new HttpResponseMessage(HttpStatusCode.NotFound); // older qrbound server
                if (Gone) return Json(HttpStatusCode.NotFound, new { error = "not_found", error_description = "Sign-in request not found" });
                LastAuthorize = JsonDocument.Parse(body);
                var root = LastAuthorize.RootElement;
                Assert.Equal("poll-secret", root.GetProperty("pollSecret").GetString());
                NonceOverride ??= root.GetProperty("nonce").GetString();
                if (Approved && AccessOutcome != null)
                    return Json(HttpStatusCode.OK, new { status = AccessOutcome, expiresAt = DateTime.UtcNow.AddMinutes(3), message = "Waiting for Eat in Houston" });
                return Json(HttpStatusCode.OK, Approved
                    ? new { status = "Approved", expiresAt = DateTime.UtcNow.AddMinutes(3), code = (string?)"the-code", state = OverrideState ?? root.GetProperty("state").GetString() }
                    : new { status = "Pending", expiresAt = DateTime.UtcNow.AddMinutes(3), code = (string?)null, state = (string?)null });
            }

            if (path == "/api/sso/token")
            {
                Assert.Equal("application/x-www-form-urlencoded", request.Content!.Headers.ContentType!.MediaType);
                TokenForm = QrboundClient.ParseQuery(body);
                return Json(HttpStatusCode.OK, new
                {
                    access_token = tokens.Create("access", null),
                    id_token = tokens.Create("id", NonceOverride),
                    token_type = "Bearer",
                    expires_in = 3600
                });
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        private static HttpResponseMessage Json(HttpStatusCode status, object value) =>
            new(status) { Content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json") };
    }
}
