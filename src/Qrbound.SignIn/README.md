# Qrbound.SignIn

Let users **log in to, or create an account for, your app with qrbound**: they scan a QR code with
their phone, tap the matching number and confirm with their passkey. It works like "Sign in with …"
SSO, without passwords.

Two packages:

| Package | Use it in | What it gives you |
|---|---|---|
| `Qrbound.SignIn` | Blazor WebAssembly, Blazor Server / Web App | `<QrboundSignIn>`, `<QrboundRegister>`, `<QrboundCallback>`, `QrboundClient`, PKCE helpers |
| `Qrbound.SignIn.Server` | ASP.NET Core backends | Token validation against qrbound's signing keys, code exchange with a client secret, cookie sign-in endpoint |

## How it works

The library uses qrbound's existing SSO protocol: an OAuth 2.0 authorization code with PKCE (S256).

1. `<QrboundSignIn>` creates a QR sign-in request for your `client_id` and `redirect_uri`, and makes a
   fresh PKCE verifier. The verifier never leaves your app.
2. The user scans the QR code with a phone that is signed in to qrbound, picks the number your page
   shows, and approves with a passkey.
3. The component polls the request with its private poll secret and the PKCE challenge. Once the phone
   approves, qrbound returns a **one-time authorization code** bound to your client, redirect URI and challenge.
4. The code plus the verifier is exchanged for an ID token and an access token (RS256, `aud` = your client_id).
   No client secret is ever needed in the browser.

The request expires after about 3 minutes. The component renews it automatically twice, then offers
"New QR code". If the user denies on the phone or picks the wrong number, the request is cancelled.
"Use a passkey on this device instead" falls back to qrbound's normal sign-in page (full redirect).

## 1. Register your app in qrbound

In the qrbound portal open **Businesses → your business → Applications → New application**:

* **Public** (no secret) for browser-only apps such as Blazor WebAssembly. These must use PKCE, which
  this library always does.
* **Confidential** (with secret) if your backend does the exchange (`Qrbound.SignIn.Server`).
* Add your exact redirect URI, e.g. `https://app.example.com/qrbound/callback`.
* Turn on **Require membership** to allow only members of your business. Their role arrives in the
  `business_role` claim.

## 2. Blazor WebAssembly (public client)

```bash
dotnet add package Qrbound.SignIn
```

`wwwroot/appsettings.json` (all values here are public):

```json
{
  "Qrbound": {
    "Authority": "https://webapi.qrbound.com",
    "PortalBaseUrl": "https://www.qrbound.com",
    "ClientId": "your-client-id",
    "RedirectUri": "https://app.example.com/qrbound/callback"
  }
}
```

`Program.cs`:

```csharp
builder.Services.AddQrboundSignIn(
    o => builder.Configuration.GetSection("Qrbound").Bind(o),
    useAuthenticationStateProvider: true); // makes <AuthorizeView> / [Authorize] work
```

A page:

```razor
<QrboundSignIn Title="Log in with qrbound" OnSignedIn="OnSignedIn" />
@* or, for new users: *@
<QrboundRegister Title="Create your account with qrbound" OnSignedIn="OnSignedIn" />

@code {
    [Inject] QrboundAuthState Auth { get; set; } = default!;

    void OnSignedIn(QrboundSignInResult result)
    {
        // result.User: Subject, Email, Name, BusinessRole, ...
        // Auth.AccessToken: bearer token for your own API (validate it there, see below)
    }
}
```

The page at your redirect URI (used by the redirect fallback):

```razor
@page "/qrbound/callback"
<QrboundCallback />
```

Sign out with `QrboundAuthState.SignOut()`. In Direct mode the session is kept in memory, so a page
reload signs the user out. Use Backend mode (below) for a session that survives reloads.

## 3. Apps with a backend (recommended for production)

The browser still shows the QR code, but it hands the code and verifier to **your** server. The server
exchanges them (with the client secret, if the app is confidential), checks the ID token signature and
sets your own cookie. Tokens never reach the browser.

```bash
dotnet add package Qrbound.SignIn.Server
```

```csharp
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie();
builder.Services.AddQrboundServer(o =>
{
    builder.Configuration.GetSection("Qrbound").Bind(o);
    // The secret comes from user secrets / environment variables (e.g. Qrbound__ClientSecret), never from code.
    o.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
});

app.UseAuthentication();
app.UseAuthorization();
app.MapQrboundSignIn();   // POST /qrbound/exchange and POST /qrbound/signout
```

Front end:

```csharp
builder.Services.AddQrboundSignIn(o =>
{
    builder.Configuration.GetSection("Qrbound").Bind(o);
    o.TokenExchange = QrboundTokenExchange.Backend;   // posts to /qrbound/exchange
});
```

### Validating qrbound access tokens in your API

```csharp
var validator = app.Services.GetRequiredService<QrboundTokenValidator>();
ClaimsPrincipal user = await validator.ValidateAccessTokenAsync(token);
```

You can also use the standard JWT bearer handler:

```csharp
builder.Services.AddAuthentication().AddJwtBearer(o =>
{
    o.MetadataAddress = "https://webapi.qrbound.com/.well-known/qr-sso-configuration";
    o.TokenValidationParameters.ValidIssuer = "https://webapi.qrbound.com";
    o.TokenValidationParameters.ValidAudience = "your-client-id";
    o.MapInboundClaims = false;
});
```

## Options

| Option | Default | |
|---|---|---|
| `Authority` | `https://webapi.qrbound.com` | qrbound API |
| `PortalBaseUrl` | `https://www.qrbound.com` | qrbound sign-in pages (redirect fallback) |
| `ClientId` | (required) | your application's client_id |
| `RedirectUri` | (required) | exact registered redirect URI |
| `Issuer` | = `Authority` | expected `iss` |
| `TokenExchange` | `Direct` | `Backend` to exchange on your server |
| `BackendExchangePath` | `/qrbound/exchange` | your backend endpoint in Backend mode |
| `PollInterval` | 2 s | used when qrbound does not suggest one |

Component parameters: `Mode`, `Title`, `CssClass`, `ShowRedirectFallback`, `ReturnUrl`,
`AutoRenewCount`, `OnSignedIn`, `OnError`. `<QrboundCallback>` also takes `DefaultReturnUrl`,
`FailureUrl` and `ForceLoad`. Restyle with the `qrbound-*` CSS classes, or set `--qrbound-accent`.

### Hosting in MVC or Razor Pages

The components also run as interactive server components inside MVC views
(`<component type="typeof(QrboundSignIn)" render-mode="Server" />`, with `AddServerSideBlazor()`,
`MapBlazorHub()`, `blazor.server.js` and `<base href="~/" />` on that page). There is no Blazor router, so:

* Set `ForceLoad="true"` on `<QrboundCallback>` when its return URL is a server endpoint (such as one that
  sets your cookie). Otherwise only the address bar changes and the endpoint never runs.
* In `QrboundSignIn`'s `OnSignedIn`, navigate with `NavigationManager.NavigateTo(url, forceLoad: true)`.
* Backend mode does not work here: the component runs on the server, so the cookie set by `/qrbound/exchange`
  would never reach the browser. Use Direct mode, validate `result.Tokens.IdToken` with
  `QrboundTokenValidator` on the server, and set your cookie from a normal request.

## Matching qrbound users to your accounts

* **Key accounts on `Subject` (`sub`)**, the qrbound user ID. It never changes; the email can.
* qrbound sends the account email (`Email`) and whether the user has confirmed it (`EmailVerified`). The
  user's "Show on card" choices don't affect sign-in: the app they approve always gets their name and email.
* To connect qrbound to accounts that already exist in your app, match by email **only when
  `EmailVerified` is true**, then store the `sub` against that account (in ASP.NET Identity:
  `UserManager.AddLoginAsync(user, new UserLoginInfo("qrbound", sub, "qrbound"))`) and use
  `FindByLoginAsync("qrbound", sub)` from then on. Don't link an account that is already linked to a
  different `sub`.
* If `EmailVerified` is false, ask the user to confirm their email in qrbound and try again, or check
  `/api/sso/userinfo` with their access token until it turns true.

## Security notes

* Browser apps are public clients: they prove possession with PKCE, never with a secret.
* In Direct mode the ID token's issuer, audience, expiry, nonce and `token_use` are checked, but not its
  signature: .NET on WebAssembly has no RSA. That is allowed because the token comes straight from
  qrbound's token endpoint over HTTPS (OpenID Connect Core 3.1.3.7). Any server that receives a token
  from a browser must validate it fully with `QrboundTokenValidator`.
* Codes are single-use, expire after 2 minutes, and are bound to client_id, redirect URI and PKCE challenge.
* The redirect fallback keeps `state`, `nonce` and the verifier in `sessionStorage` (single use, 15 minutes).

## Sample

`samples/Qrbound.SignIn.Sample` in the qrbound repository is a Blazor WebAssembly app at
`https://localhost:7181`. Put a public application's client_id in its `wwwroot/appsettings.Development.json`,
register `https://localhost:7181/qrbound/callback` as a redirect URI, then start the API, the portal
and the sample.
