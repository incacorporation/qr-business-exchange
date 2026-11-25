using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using QRBusinessExchange.Core.DTOs;
using QRBusinessExchange.Core.Entities;
using QRBusinessExchange.Infrastructure.Configuration;
using QRBusinessExchange.Infrastructure.Data;

namespace QRBusinessExchange.Infrastructure.Services;

/// <summary>
/// The single sign-on flow for business applications:
/// <list type="number">
/// <item>The app sends the user to the portal's /sso/authorize page.</item>
/// <item>The user signs in with a passkey (directly, or by approving a QR code on their phone).</item>
/// <item>The portal asks <see cref="CreateAuthorizationCodeAsync"/> for a one-time code and redirects back.</item>
/// <item>The app exchanges the code (plus client secret and/or PKCE verifier) in <see cref="ExchangeCodeAsync"/>
/// for RS256-signed JWTs whose audience is the app's client_id.</item>
/// </list>
/// </summary>
public class SsoService
{
    /// <summary>Authentication methods accepted for SSO. Passwords are deliberately excluded.</summary>
    public static readonly string[] AllowedAuthMethods = { "passkey", "qr" };

    private readonly ApplicationDbContext _db;
    private readonly SsoSigningKeyProvider _keys;
    private readonly SsoSettings _settings;
    private readonly SsoAccessService _access;
    private readonly JwtSecurityTokenHandler _handler = new() { MapInboundClaims = false };

    public SsoService(ApplicationDbContext db, SsoSigningKeyProvider keys, IOptions<SsoSettings> settings,
        SsoAccessService access)
    {
        _db = db;
        _access = access;
        _keys = keys;
        _settings = settings.Value;
    }

    /// <summary>
    /// Public information about an application for the sign-in screen. When a redirect URI
    /// is supplied it must be registered for the app, otherwise the request is rejected.
    /// </summary>
    public async Task<SsoClientInfoDto> GetClientInfoAsync(string clientId, string? redirectUri)
    {
        var app = await GetActiveApplicationAsync(clientId);

        if (redirectUri != null && !app.GetRedirectUris().Contains(redirectUri, StringComparer.Ordinal))
        {
            throw new SsoException("invalid_redirect_uri", "The redirect URI is not registered for this application");
        }

        return new SsoClientInfoDto
        {
            ClientId = app.ClientId,
            ApplicationName = app.Name,
            BusinessName = app.Business.Name,
            BusinessLogoUrl = app.Business.LogoUrl,
            BusinessWebsiteUrl = app.Business.WebsiteUrl,
            RequireMembership = app.RequireMembership,
            IsConfidential = app.IsConfidential
        };
    }

    /// <summary>
    /// Issues a one-time authorization code for a signed-in user and returns the URL
    /// to send the browser back to.
    /// </summary>
    public async Task<SsoAuthorizeResponse> CreateAuthorizationCodeAsync(
        Guid userId, string authMethod, DateTime authTime, SsoAuthorizeRequest request)
    {
        var code = await IssueAuthorizationCodeAsync(userId, authMethod, authTime, request);

        var separator = request.RedirectUri.Contains('?') ? '&' : '?';
        var redirect = $"{request.RedirectUri}{separator}code={Uri.EscapeDataString(code)}";
        if (!string.IsNullOrEmpty(request.State))
        {
            redirect += $"&state={Uri.EscapeDataString(request.State)}";
        }

        return new SsoAuthorizeResponse { RedirectUrl = redirect };
    }

    /// <summary>
    /// Checks everything about an authorization request that does not depend on the user:
    /// the application is active, the redirect URI is registered, and PKCE is valid
    /// (and present for public applications).
    /// </summary>
    public async Task<SsoApplication> ValidateAuthorizeRequestAsync(SsoAuthorizeRequest request)
    {
        var app = await GetActiveApplicationAsync(request.ClientId);

        if (!app.GetRedirectUris().Contains(request.RedirectUri, StringComparer.Ordinal))
        {
            throw new SsoException("invalid_redirect_uri", "The redirect URI is not registered for this application");
        }

        ValidatePkce(request.CodeChallenge, request.CodeChallengeMethod, app.IsConfidential);
        return app;
    }

    /// <summary>PKCE rules: S256 only, 43-128 characters, required for public applications.</summary>
    public static void ValidatePkce(string? codeChallenge, string? codeChallengeMethod, bool isConfidential)
    {
        if (!string.IsNullOrEmpty(codeChallenge))
        {
            if (!string.Equals(codeChallengeMethod ?? "S256", "S256", StringComparison.Ordinal))
            {
                throw new SsoException("invalid_request", "Only the S256 code_challenge_method is supported");
            }

            if (codeChallenge.Length is < 43 or > 128)
            {
                throw new SsoException("invalid_request", "code_challenge has an invalid length");
            }
        }
        else if (!isConfidential)
        {
            throw new SsoException("invalid_request", "Public applications must use PKCE (code_challenge)");
        }
    }

    /// <summary>
    /// Issues a one-time authorization code for a signed-in user and returns the raw code
    /// (only its hash is stored).
    /// </summary>
    public async Task<string> IssueAuthorizationCodeAsync(
        Guid userId, string authMethod, DateTime authTime, SsoAuthorizeRequest request)
    {
        if (!AllowedAuthMethods.Contains(authMethod))
        {
            throw new SsoException("passkey_required", "Sign in with a passkey or QR code to continue", 403);
        }

        var app = await ValidateAuthorizeRequestAsync(request);

        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId)
            ?? throw new SsoException("access_denied", "User not found", 403);

        if (!user.IsActive)
        {
            throw new SsoException("access_denied", "Account is deactivated", 403);
        }

        if (app.RequireMembership &&
            !await _db.BusinessMembers.AnyAsync(m => m.BusinessId == app.BusinessId && m.UserId == userId))
        {
            throw new SsoException("access_denied", $"Your account is not a member of {app.Business.Name}", 403);
        }

        // Application categories: pending, rejected or missing access stops here (access_pending / access_required / access_denied)
        await _access.EnsureCanSignInAsync(app, userId, request.Category);

        var code = SsoCrypto.NewSecret();
        _db.SsoAuthorizationCodes.Add(new SsoAuthorizationCode
        {
            CodeHash = SsoCrypto.Hash(code),
            SsoApplicationId = app.SsoApplicationId,
            UserId = userId,
            RedirectUri = request.RedirectUri,
            CodeChallenge = request.CodeChallenge,
            CodeChallengeMethod = string.IsNullOrEmpty(request.CodeChallenge) ? null : "S256",
            Nonce = request.Nonce,
            AuthMethod = authMethod,
            AuthTime = authTime,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddSeconds(_settings.AuthorizationCodeLifetimeSeconds)
        });

        // Opportunistic cleanup of old codes
        var cutoff = DateTime.UtcNow.AddHours(-1);
        await _db.SsoAuthorizationCodes.Where(c => c.ExpiresAt < cutoff).ExecuteDeleteAsync();

        await _db.SaveChangesAsync();

        return code;
    }

    /// <summary>
    /// Exchanges an authorization code for tokens (server-to-server or PKCE public client).
    /// </summary>
    public async Task<SsoTokenResponse> ExchangeCodeAsync(
        string? grantType, string? code, string? redirectUri, string? clientId, string? clientSecret, string? codeVerifier)
    {
        if (grantType != "authorization_code")
        {
            throw new SsoException("unsupported_grant_type", "Only grant_type=authorization_code is supported");
        }

        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(redirectUri))
        {
            throw new SsoException("invalid_request", "code, client_id and redirect_uri are required");
        }

        var app = await _db.SsoApplications.Include(a => a.Business)
            .FirstOrDefaultAsync(a => a.ClientId == clientId);

        if (app == null || !app.IsActive || !app.Business.IsActive)
        {
            throw new SsoException("invalid_client", "Unknown or disabled client", 401);
        }

        if (app.IsConfidential && !SsoCrypto.Matches(clientSecret, app.ClientSecretHash))
        {
            throw new SsoException("invalid_client", "Client authentication failed", 401);
        }

        var codeHash = SsoCrypto.Hash(code);
        var record = await _db.SsoAuthorizationCodes.Include(c => c.User)
            .FirstOrDefaultAsync(c => c.CodeHash == codeHash);

        if (record == null || record.SsoApplicationId != app.SsoApplicationId ||
            record.ConsumedAt != null || record.ExpiresAt < DateTime.UtcNow ||
            !string.Equals(record.RedirectUri, redirectUri, StringComparison.Ordinal))
        {
            throw new SsoException("invalid_grant", "The authorization code is invalid, expired, or already used");
        }

        if (!string.IsNullOrEmpty(record.CodeChallenge))
        {
            if (string.IsNullOrEmpty(codeVerifier) ||
                !string.Equals(SsoCrypto.PkceS256(codeVerifier), record.CodeChallenge, StringComparison.Ordinal))
            {
                throw new SsoException("invalid_grant", "PKCE verification failed");
            }
        }

        record.ConsumedAt = DateTime.UtcNow;
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new SsoException("invalid_grant", "The authorization code has already been used");
        }

        var user = record.User;
        if (!user.IsActive)
        {
            throw new SsoException("invalid_grant", "Account is deactivated");
        }

        var membershipRole = await _db.BusinessMembers
            .Where(m => m.BusinessId == app.BusinessId && m.UserId == user.UserId)
            .Select(m => m.Role)
            .FirstOrDefaultAsync();

        // Named roles in the app's business. SuperAdmin is QRBound's own platform role, so apps never receive it.
        var businessRoles = await _db.BusinessRoles
            .Where(r => r.BusinessId == app.BusinessId && r.UserId == user.UserId && r.RoleName != BusinessRoleNames.SuperAdmin)
            .Select(r => r.RoleName)
            .OrderBy(r => r)
            .ToListAsync();
        membershipRole = BusinessAccess.Combine(membershipRole,
            businessRoles.Contains(BusinessRoleNames.Administrator), isSuperAdmin: false);

        var lifetime = TimeSpan.FromMinutes(app.TokenLifetimeMinutes);
        var (categories, actingFor) = await _access.GetClaimsAsync(app, user.UserId);
        var claims = BuildUserClaims(user, app, membershipRole, record.AuthMethod, record.AuthTime, categories, actingFor);
        // One claim per role, so several become a JSON array
        claims.AddRange(businessRoles.Select(r => new Claim("business_roles", r)));

        var accessToken = CreateJwt(app.ClientId, lifetime, claims.Append(new Claim("token_use", "access")));

        var idClaims = claims.Append(new Claim("token_use", "id"));
        if (!string.IsNullOrEmpty(record.Nonce))
        {
            idClaims = idClaims.Append(new Claim("nonce", record.Nonce));
        }
        var idToken = CreateJwt(app.ClientId, lifetime, idClaims);

        return new SsoTokenResponse
        {
            AccessToken = accessToken,
            IdToken = idToken,
            TokenType = "Bearer",
            ExpiresIn = (int)lifetime.TotalSeconds
        };
    }

    /// <summary>
    /// Validates an access token issued to any application and returns its claims.
    /// </summary>
    public async Task<Dictionary<string, object?>> GetUserInfoAsync(string accessToken)
    {
        ClaimsPrincipal principal;
        try
        {
            principal = _handler.ValidateToken(accessToken, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = _settings.Issuer,
                ValidateAudience = false, // checked below against registered apps
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = _keys.SecurityKey,
                ValidAlgorithms = new[] { SecurityAlgorithms.RsaSha256 },
                ClockSkew = TimeSpan.FromSeconds(30)
            }, out _);
        }
        catch (Exception)
        {
            throw new SsoException("invalid_token", "The access token is invalid or expired", 401);
        }

        if (principal.FindFirst("token_use")?.Value != "access")
        {
            throw new SsoException("invalid_token", "An access token is required", 401);
        }

        var clientId = principal.FindFirst("client_id")?.Value;
        if (clientId == null || !await _db.SsoApplications.AnyAsync(a => a.ClientId == clientId && a.IsActive))
        {
            throw new SsoException("invalid_token", "The application is no longer active", 401);
        }

        var userId = Guid.Parse(principal.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);
        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId && u.IsActive)
            ?? throw new SsoException("invalid_token", "User not found", 401);

        return new Dictionary<string, object?>
        {
            ["sub"] = user.UserId.ToString(),
            ["email"] = user.Email,
            ["email_verified"] = user.IsEmailVerified,
            ["name"] = $"{user.FirstName} {user.LastName}".Trim(),
            ["given_name"] = user.FirstName,
            ["family_name"] = user.LastName,
            ["picture"] = user.ProfileImageUrl,
            ["company"] = user.CompanyName,
            ["business_id"] = principal.FindFirst("business_id")?.Value,
            ["business_role"] = principal.FindFirst("business_role")?.Value,
            ["business_roles"] = principal.FindAll("business_roles").Select(c => c.Value).ToArray(),
            ["category"] = principal.FindAll("category").Select(c => c.Value).ToArray(),
            ["org_id"] = principal.FindFirst("org_id")?.Value,
            ["org_slug"] = principal.FindFirst("org_slug")?.Value,
            ["org_name"] = principal.FindFirst("org_name")?.Value
        };
    }

    /// <summary>
    /// Discovery document describing the SSO endpoints.
    /// </summary>
    public object GetConfiguration(string apiBaseUrl) => new
    {
        issuer = _settings.Issuer,
        authorization_endpoint = $"{_settings.PortalBaseUrl.TrimEnd('/')}/sso/authorize",
        token_endpoint = $"{apiBaseUrl}/api/sso/token",
        userinfo_endpoint = $"{apiBaseUrl}/api/sso/userinfo",
        jwks_uri = $"{apiBaseUrl}/api/sso/jwks",
        end_session_endpoint = $"{_settings.PortalBaseUrl.TrimEnd('/')}/sso/logout",
        qr_login_endpoint = $"{apiBaseUrl}/api/qr-login/sessions",
        qr_authorize_endpoint = $"{apiBaseUrl}/api/qr-login/sessions/{{session_id}}/authorize",
        response_types_supported = new[] { "code" },
        grant_types_supported = new[] { "authorization_code" },
        code_challenge_methods_supported = new[] { "S256" },
        token_endpoint_auth_methods_supported = new[] { "client_secret_post", "none" },
        id_token_signing_alg_values_supported = new[] { "RS256" },
        amr_values_supported = AllowedAuthMethods,
        claims_supported = new[]
        {
            "sub", "email", "email_verified", "name", "given_name", "family_name",
            "amr", "auth_time", "nonce", "client_id", "business_id", "business_slug", "business_role",
            "business_roles", "category", "org_id", "org_slug", "org_name"
        }
    };

    public object GetJwks() => new { keys = new[] { _keys.Jwk } };

    // -----------------------------------------------------------------------

    private async Task<SsoApplication> GetActiveApplicationAsync(string clientId)
    {
        var app = await _db.SsoApplications.Include(a => a.Business)
            .FirstOrDefaultAsync(a => a.ClientId == clientId);

        if (app == null || !app.IsActive || !app.Business.IsActive)
        {
            throw new SsoException("invalid_client", "Unknown or disabled application", 400);
        }

        return app;
    }

    private static List<Claim> BuildUserClaims(
        User user, SsoApplication app, string? membershipRole, string authMethod, DateTime authTime,
        IReadOnlyList<string> categories, Business? actingFor)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new("email_verified", user.IsEmailVerified ? "true" : "false", ClaimValueTypes.Boolean),
            new("name", $"{user.FirstName} {user.LastName}".Trim()),
            new(JwtRegisteredClaimNames.GivenName, user.FirstName),
            new(JwtRegisteredClaimNames.FamilyName, user.LastName),
            new(JwtRegisteredClaimNames.Amr, authMethod),
            new(JwtRegisteredClaimNames.AuthTime, new DateTimeOffset(DateTime.SpecifyKind(authTime, DateTimeKind.Utc)).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new("client_id", app.ClientId),
            new("business_id", app.BusinessId.ToString()),
            new("business_slug", app.Business.Slug)
        };

        if (membershipRole != null)
        {
            claims.Add(new Claim("business_role", membershipRole));
        }

        // The application's categories this user holds ("client", "business", ...); one claim each, so
        // several become a JSON array
        claims.AddRange(categories.Select(c => new Claim("category", c)));

        // The user's own business they act for (a category with RequiresBusiness)
        if (actingFor != null)
        {
            claims.Add(new Claim("org_id", actingFor.BusinessId.ToString()));
            claims.Add(new Claim("org_slug", actingFor.Slug));
            claims.Add(new Claim("org_name", actingFor.Name));
        }

        return claims;
    }

    private string CreateJwt(string audience, TimeSpan lifetime, IEnumerable<Claim> claims)
    {
        var now = DateTime.UtcNow;
        var all = claims.Append(new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())).ToList();

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: audience,
            claims: all,
            notBefore: now,
            expires: now.Add(lifetime),
            signingCredentials: _keys.SigningCredentials);

        return _handler.WriteToken(token);
    }
}
