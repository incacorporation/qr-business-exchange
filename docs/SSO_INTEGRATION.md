# QR Code / Passkey SSO Core — Multi-Business Integration

QR Business Exchange now acts as a **sign-in core for many businesses**. Each business registers
its own applications; their users sign in **only with passkeys** (WebAuthn) — either on the same
device, or by **scanning a QR code with a phone that is already signed in**. Applications receive
**RS256-signed JWTs** they can validate offline.

The existing business-card features (connections, map, payments, subscriptions) are unchanged;
the portal itself is simply one more consumer of the core.

---

## Concepts

| Concept | Table | Notes |
|---|---|---|
| Business (tenant) | `Businesses` | Created by any account; creator becomes **Owner**. |
| Member | `BusinessMembers` | Roles: `Owner`, `Admin`, `Member`. Owners/Admins manage apps and members. |
| Application | `SsoApplications` | Has a `client_id`, exact-match redirect URIs, optional client secret. |
| Authorization code | `SsoAuthorizationCodes` | Single use, 120 s, stored as SHA-256 hash only. |
| QR login session | `QrLoginSessions` | 180 s, poll secret stored as SHA-256 hash only. |

Manage everything in the portal under **Businesses & SSO** (`/businesses`), or via
`/api/businesses/...` (see Swagger).

### Application types

* **Confidential** (has a backend): gets a `client_secret`, shown once. PKCE optional but recommended.
* **Public** (SPA / mobile): no secret; **PKCE S256 is mandatory**.
* **Members only**: when enabled, only members of the owning business can sign in.

---

## Sign-in flow

```
App ──(1) redirect──▶ Portal /sso/authorize
                         │  (2) passkey on this device  OR  scan QR with signed-in phone
                         ▼
App ◀─(3) redirect_uri?code=…&state=…
App ──(4) POST /api/sso/token (code + secret/verifier) ──▶ API
App ◀─(5) { access_token, id_token, expires_in }   (RS256 JWT, aud = client_id)
```

### 1. Redirect the user

```
https://<portal>/sso/authorize
    ?client_id=qrb_xxxxxxxxxxxxxxxxxxxx
    &redirect_uri=https://app.example.com/auth/callback
    &state=<random, check on return>
    &code_challenge=<BASE64URL(SHA256(code_verifier))>
    &code_challenge_method=S256
    &nonce=<optional, echoed in id_token>
    &prompt=login            (optional: force a fresh passkey/QR sign-in)
```

An unknown `client_id` or unregistered `redirect_uri` shows an error page and **never redirects**.

### 2. The user authenticates

* **Passkey** – Windows Hello, Touch ID / Face ID, Android, or a security key.
* **QR code** – the page shows a QR code and a two-digit number. The user scans it with a phone
  already signed in to the portal, taps the matching number, and confirms with a passkey. A wrong
  pick blocks the request (protects against forwarded/phished QR codes).
* An existing portal session is reused ("Continue as …") only if it was created by passkey or QR.
  Password sessions must step up with a passkey — **passwords never satisfy SSO**.

### 3. Handle the callback

Success: `redirect_uri?code=…&state=…`
Error / cancel: `redirect_uri?error=access_denied&error_description=…&state=…`

### 4. Exchange the code

`POST https://<api>/api/sso/token` — form-encoded (or JSON with the same keys).
Client credentials may also be sent via HTTP Basic.

```bash
curl -X POST https://localhost:7059/api/sso/token \
  -d grant_type=authorization_code \
  -d code=$CODE \
  -d redirect_uri=https://app.example.com/auth/callback \
  -d client_id=qrb_xxxxxxxxxxxxxxxxxxxx \
  -d client_secret=$CLIENT_SECRET \
  -d code_verifier=$CODE_VERIFIER
```

```json
{ "access_token": "eyJ…", "id_token": "eyJ…", "token_type": "Bearer", "expires_in": 3600 }
```

Errors use OAuth style: `{ "error": "invalid_grant", "error_description": "…" }`.

### 5. Validate the token

* Algorithm: **RS256**; keys: `GET /api/sso/jwks`
* `iss` = `Sso:Issuer` (default `https://localhost:7059`), `aud` = your `client_id`
* Claims: `sub`, `email`, `email_verified`, `name`, `given_name`, `family_name`,
  `amr` (`passkey` | `qr`), `auth_time`, `client_id`, `business_id`, `business_slug`,
  `business_role` (if the user is a member), `token_use` (`access` | `id`), `nonce` (id_token).

ASP.NET Core example:

```csharp
builder.Services.AddAuthentication().AddJwtBearer(o =>
{
    // Not OIDC discovery: the signing keys come from the JWKS endpoint below
    o.MapInboundClaims = false;
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidIssuer = "https://localhost:7059",
        ValidAudience = "qrb_xxxxxxxxxxxxxxxxxxxx",
        IssuerSigningKeyResolver = (_, _, _, _) =>
            new JsonWebKeySet(new HttpClient().GetStringAsync("https://localhost:7059/api/sso/jwks").Result)
                .GetSigningKeys()                         // cache this in real code
    };
});
```

`GET /api/sso/userinfo` with `Authorization: Bearer <access_token>` returns the current profile.

### Sign-out

```
https://<portal>/sso/logout?client_id=…&post_logout_redirect_uri=<a registered redirect URI>&state=…
```

### Discovery

`GET /api/sso/configuration` (also `/.well-known/qr-sso-configuration`).

---

## Configuration (`src/QRBusinessExchange.API/appsettings.json`)

```json
"WebAuthn": { "RpId": "localhost", "RpName": "QR Business Exchange", "Origins": [ "https://localhost:60128" ] },
"Sso": {
  "Issuer": "https://localhost:7059",
  "PortalBaseUrl": "https://localhost:60128",
  "SigningKeyPath": "keys/sso-signing-key.pem",
  "AuthorizationCodeLifetimeSeconds": 120,
  "QrSessionLifetimeSeconds": 180
}
```

* `WebAuthn:RpId` must be the portal's registrable domain in production (e.g. `qrbound.com`), and
  `Origins` must list the portal origin(s). Passkeys are bound to the RP ID.
* The signing key is generated on first start if missing. In production provide a managed key
  (Key Vault / mounted secret); `**/keys/*.pem` is git-ignored.
* WebAuthn challenges live in `IMemoryCache`. Use a distributed cache before running more than one API instance.

## Database

Migration `AddMultiBusinessSso` adds the five tables above. It is applied automatically at API
start-up by `DbInitializer` (`MigrateAsync`), or manually:

```bash
dotnet ef database update --project src/QRBusinessExchange.Infrastructure
```

## Passkey fixes included

The SSO core depends on passkeys, so these existing WebAuthn bugs were fixed:

* Challenges were stored per request (scoped service) and were always "not found" → now `IMemoryCache`.
* Registration failed parsing the attestation object (`fmt`/`attStmt` read as byte strings).
* ES256 public keys failed to parse (`crv` read as bytes) and DER signatures were verified as P1363.
* Synced passkeys (signature counter always 0) were rejected as replays.
* Origin, RP ID hash, and user-presence/verification flags were not checked.
* Any signed-in user could register a passkey on **another** account (email from request body).
* Passkey list/delete looked up the user by a claim that was never set.
* The Blazor client never sent the bearer token, so authorized calls failed.
