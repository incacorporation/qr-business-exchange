using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qrbound.SignIn;
using Qrbound.SignIn.Server;

namespace Qrbound.POS.Services;

/// <summary>A customer signed in with their qrbound account.</summary>
/// <param name="Subject">The qrbound user ID (<c>sub</c>): never changes, unlike the email.</param>
/// <param name="Name">The name on the qrbound account, if any.</param>
/// <param name="Email">The qrbound account's email, if any.</param>
/// <param name="EmailVerified">Whether the customer has confirmed that email with qrbound.</param>
public sealed record PosCustomer(string Subject, string? Name, string? Email, bool EmailVerified)
{
    public string DisplayName => !string.IsNullOrWhiteSpace(Name) ? Name! : Email ?? "qrbound customer";
}

/// <summary>Who the customer on this page is, from the customer sign-in cookie.</summary>
public sealed class CurrentCustomer(AuthenticationStateProvider auth, IOptions<QrboundPosOptions> options)
{
    /// <summary>Whether customers have to be signed in to order and pay.</summary>
    public bool Required => options.Value.CustomerSignIn.Required;

    /// <summary>The signed-in customer, or null.</summary>
    public async Task<PosCustomer?> GetAsync()
    {
        var user = (await auth.GetAuthenticationStateAsync()).User;
        return CustomerSignIn.FromPrincipal(user);
    }
}

/// <summary>
/// Customer sign-in with qrbound (OAuth 2.0 authorization code + PKCE, exchanged on this server with
/// the client secret). On a phone it's a full-page redirect to qrbound's sign-in page, where the
/// customer approves with their passkey, then back to the page they scanned.
/// </summary>
public static class CustomerSignIn
{
    public const string SignInPath = "/pos/customer/signin";
    public const string CallbackPath = "/pos/customer/callback";
    public const string SignOutPath = "/pos/customer/signout";
    public const string DevSignInPath = "/pos/customer/dev-signin";
    public const string ErrorPath = "/pos/customer/signin-error";

    private const string PendingCookie = ".QrboundPos.CustomerSignIn";
    private const string ProtectorPurpose = "Qrbound.POS.CustomerSignIn.v1";
    private static readonly TimeSpan PendingLifetime = TimeSpan.FromMinutes(15);

    public const string SubjectClaim = "sub";
    public const string EmailClaim = "email";
    public const string EmailVerifiedClaim = "email_verified";

    /// <summary>The customer identity in a (merged) principal, or null.</summary>
    public static PosCustomer? FromPrincipal(ClaimsPrincipal user)
    {
        var identity = user.Identities.FirstOrDefault(i => i.IsAuthenticated && i.AuthenticationType == QrboundPosDefaults.CustomerAuthenticationScheme);
        var sub = identity?.FindFirst(SubjectClaim)?.Value;
        if (identity is null || string.IsNullOrEmpty(sub)) return null;
        return new PosCustomer(sub, identity.FindFirst(ClaimTypes.Name)?.Value, identity.FindFirst(EmailClaim)?.Value,
            string.Equals(identity.FindFirst(EmailVerifiedClaim)?.Value, "true", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// The customer cookie's identity. Only these claims are copied from qrbound: in particular no
    /// roles, so nothing a customer brings from qrbound can count as POS staff access.
    /// </summary>
    internal static ClaimsPrincipal ToPrincipal(string subject, string? name, string? email, bool emailVerified)
    {
        var claims = new List<Claim> { new(SubjectClaim, subject) };
        if (!string.IsNullOrWhiteSpace(name)) claims.Add(new Claim(ClaimTypes.Name, name.Trim()));
        if (!string.IsNullOrWhiteSpace(email)) claims.Add(new Claim(EmailClaim, email.Trim()));
        claims.Add(new Claim(EmailVerifiedClaim, emailVerified ? "true" : "false"));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, QrboundPosDefaults.CustomerAuthenticationScheme, ClaimTypes.Name, "pos_customer_role"));
    }

    /// <summary>Only ever go back to a page of this app.</summary>
    internal static string SafeReturnUrl(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && returnUrl.StartsWith('/') && !returnUrl.StartsWith("//") && !returnUrl.StartsWith("/\\")
            ? returnUrl
            : "/";

    internal static void Map(IEndpointRouteBuilder endpoints)
    {
        // Start: remember state, nonce and the PKCE verifier in a short-lived protected cookie, and send
        // the customer to qrbound.
        endpoints.MapGet(SignInPath, (HttpContext http, string? returnUrl, IOptions<QrboundPosOptions> posOptions,
            IOptions<QrboundServerOptions> serverOptions, IDataProtectionProvider protection) =>
        {
            var o = posOptions.Value.CustomerSignIn;
            var target = SafeReturnUrl(returnUrl ?? http.Request.Query["ReturnUrl"]);
            if (o.Simulate)
            {
                return Results.LocalRedirect($"~{DevSignInPath}?returnUrl={Uri.EscapeDataString(target)}");
            }

            var pending = new PendingSignIn(Pkce.CreateRandomValue(), Pkce.CreateRandomValue(), Pkce.CreateVerifier(), target, DateTimeOffset.UtcNow);
            var protector = protection.CreateProtector(ProtectorPurpose);
            http.Response.Cookies.Append(PendingCookie, protector.Protect(JsonSerializer.Serialize(pending)), new CookieOptions
            {
                HttpOnly = true,
                Secure = http.Request.IsHttps,
                SameSite = SameSiteMode.Lax, // the return from qrbound is a top-level GET
                Path = "/pos/customer",
                MaxAge = PendingLifetime,
                IsEssential = true
            });

            var server = serverOptions.Value;
            var url = QrboundClient.BuildAuthorizeUrl(new QrboundOptions
            {
                Authority = server.Authority,
                PortalBaseUrl = o.PortalBaseUrl,
                ClientId = server.ClientId,
                RedirectUri = server.RedirectUri
            }, pending.State, pending.Nonce, Pkce.CreateChallenge(pending.Verifier), category: o.Category);

            http.Response.Headers.CacheControl = "no-store";
            return Results.Redirect(url);
        });

        // Return from qrbound: check state, exchange the code (with the client secret), validate the ID
        // token's signature, issuer, audience, lifetime and nonce, then sign the customer in.
        endpoints.MapGet(CallbackPath, async (HttpContext http, IDataProtectionProvider protection,
            IServiceProvider services, ILoggerFactory loggers) =>
        {
            var logger = loggers.CreateLogger("Qrbound.POS.CustomerSignIn");
            var query = http.Request.Query;
            PendingSignIn? pending = null;
            if (http.Request.Cookies.TryGetValue(PendingCookie, out var raw))
            {
                try
                {
                    pending = JsonSerializer.Deserialize<PendingSignIn>(protection.CreateProtector(ProtectorPurpose).Unprotect(raw));
                }
                catch (Exception ex) when (ex is System.Security.Cryptography.CryptographicException or JsonException)
                {
                    pending = null;
                }
            }

            http.Response.Cookies.Delete(PendingCookie, new CookieOptions { Path = "/pos/customer" });
            var back = pending?.ReturnUrl ?? "/";

            IResult Fail(string reason) =>
                Results.LocalRedirect($"~{ErrorPath}?reason={Uri.EscapeDataString(reason)}&returnUrl={Uri.EscapeDataString(back)}");

            if (pending is null || DateTimeOffset.UtcNow - pending.CreatedAt > PendingLifetime)
            {
                return Fail("expired");
            }

            if (!string.Equals(query["state"], pending.State, StringComparison.Ordinal))
            {
                logger.LogWarning("qrbound sign-in callback with a state that doesn't match");
                return Fail("state");
            }

            var error = query["error"].ToString();
            if (!string.IsNullOrEmpty(error))
            {
                return Fail(error is "access_denied" or "access_pending" ? error : "denied");
            }

            var code = query["code"].ToString();
            if (string.IsNullOrEmpty(code)) return Fail("denied");

            try
            {
                var client = services.GetRequiredService<QrboundServerClient>();
                var validator = services.GetRequiredService<QrboundTokenValidator>();
                var tokens = await client.ExchangeCodeAsync(code, pending.Verifier, http.RequestAborted);
                var principal = await validator.ValidateIdTokenAsync(tokens.IdToken, pending.Nonce, http.RequestAborted);

                string? Get(string type) => principal.FindFirst(type)?.Value;
                var sub = Get("sub");
                if (string.IsNullOrEmpty(sub)) return Fail("invalid");

                var name = Get("name") ?? string.Join(' ', new[] { Get("given_name"), Get("family_name") }.Where(s => !string.IsNullOrWhiteSpace(s)));
                var customer = ToPrincipal(sub, name, Get("email"), string.Equals(Get("email_verified"), "true", StringComparison.OrdinalIgnoreCase));
                await http.SignInAsync(QrboundPosDefaults.CustomerAuthenticationScheme, customer,
                    new AuthenticationProperties { IsPersistent = false });
                return Results.LocalRedirect(pending.ReturnUrl);
            }
            catch (QrboundException ex)
            {
                logger.LogWarning("qrbound code exchange refused: {Error} {Description}", ex.Error, ex.Message);
                return Fail("exchange");
            }
            catch (QrboundTokenException ex)
            {
                logger.LogWarning("qrbound ID token rejected: {Message}", ex.Message);
                return Fail("invalid");
            }
            catch (HttpRequestException ex)
            {
                logger.LogWarning(ex, "qrbound couldn't be reached for the sign-in");
                return Fail("unreachable");
            }
        });

        endpoints.MapPost(SignOutPath, async (HttpContext http, string? returnUrl) =>
        {
            await http.SignOutAsync(QrboundPosDefaults.CustomerAuthenticationScheme);
            return Results.LocalRedirect("~" + SafeReturnUrl(returnUrl));
        });
    }

    private sealed record PendingSignIn(string State, string Nonce, string Verifier, string ReturnUrl, DateTimeOffset CreatedAt);
}

/// <summary>
/// The POS's default scheme: reads both the staff and the customer cookie and puts both identities in
/// one principal, so pages (and the interactive circuit) see whoever is signed in. Policies still name
/// the one scheme they accept, and challenges go to that scheme's sign-in page.
/// </summary>
internal sealed class PosCombinedAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "QrboundPos";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var staff = await Context.AuthenticateAsync(QrboundPosDefaults.AuthenticationScheme);
        var customer = await Context.AuthenticateAsync(QrboundPosDefaults.CustomerAuthenticationScheme);

        var identities = new List<ClaimsIdentity>();
        if (staff.Succeeded) identities.AddRange(staff.Principal!.Identities);
        if (customer.Succeeded) identities.AddRange(customer.Principal!.Identities);
        if (identities.Count == 0) return AuthenticateResult.NoResult();

        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identities), SchemeName));
    }

    // Anything without its own scheme is staff territory.
    protected override Task HandleChallengeAsync(AuthenticationProperties properties) =>
        Context.ChallengeAsync(QrboundPosDefaults.AuthenticationScheme, properties);

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties) =>
        Context.ForbidAsync(QrboundPosDefaults.AuthenticationScheme, properties);
}
