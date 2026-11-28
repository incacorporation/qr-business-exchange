# Qrbound.POS

A restaurant point of sale for Blazor Web Apps (.NET 10, Interactive Server), on SQL Server.

- **Customers** scan the QR code on their table, order from the menu on their phone (options such
  as size and extras, notes for the kitchen), and pay by card through **Qrbound.Payments**, with a tip.
- **Staff** use the register, a live kitchen screen, today's orders and takings, the menu editor,
  tables with printable QR codes, and restaurant settings. One install runs several restaurants.

## Set up

```csharp
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddQrboundPos(builder.Configuration, builder.Environment);

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapQrboundPos();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(Qrbound.POS.QrboundPosOptions).Assembly);

await app.InitializeQrboundPosDatabaseAsync(); // creates / upgrades the POS tables
app.Run();
```

Add the same assembly to your `Router`'s `AdditionalAssemblies`, and link the stylesheet in `App.razor`:
`<link rel="stylesheet" href="@Assets["_content/Qrbound.POS/pos.css"]" />`. The pages set their own
render modes, so leave the router static. `samples/Qrbound.POS.Sample` is a complete host.

## Choosing the database

The POS keeps its tables in the `pos` schema, so it can have a database to itself or share one
your app already uses. Which database is configuration:

```json
"ConnectionStrings": { "QrboundPos": "Server=...;Database=...;User Id=...;Encrypt=True" },
"QrboundPos": {
  "ConnectionStringName": "QrboundPos",
  "ApplyMigrationsOnStartup": true,
  "CommandTimeoutSeconds": 30
}
```

| Setting | |
| --- | --- |
| `ConnectionStringName` | Which `ConnectionStrings` entry to use (default `QrboundPos`). Point it at an existing entry to share that database. |
| `ConnectionString` | The connection string itself; wins over `ConnectionStringName`. |
| `ApplyMigrationsOnStartup` | Create or upgrade the tables at startup (default true). Turn off and run `dotnet ef database update` yourself if your database login can't change the schema. |
| `CommandTimeoutSeconds` | SQL command timeout. |

Keep passwords in user secrets or environment variables (`ConnectionStrings__QrboundPos`), never in
appsettings.json.

## Customers sign in with qrbound

Customers have to be signed in to their qrbound account to see the menu, order and pay. Scanning a
table's QR code on a phone that isn't signed in yet goes to qrbound's sign-in page (passkey), then
straight back to that table's menu. Each order belongs to the qrbound account that placed it: only
that customer can open its page. A register order's "scan to pay" link belongs to the first customer
who opens it. Staff see the customer's name and qrbound email on the orders page.

The POS is a **confidential qrbound SSO application**. In the qrbound portal open Businesses → your
business → Applications → New application, choose Confidential, and add the redirect URI
`https://your-pos-host/pos/customer/callback`. Then:

```json
"QrboundPos": {
  "PublicBaseUrl": "https://your-pos-host/",
  "CustomerSignIn": {
    "Required": true,
    "ClientId": "your-client-id",
    "ClientSecret": "(user secrets)",
    "PortalBaseUrl": "https://www.qrbound.com",
    "Category": null
  }
}
```

| Setting | |
| --- | --- |
| `Required` | Customers must sign in (default true). Off: anyone with the link can order. |
| `ClientId`, `ClientSecret` | The application's credentials; the secret stays on the server. |
| `RedirectUri` | Defaults to `PublicBaseUrl` + `pos/customer/callback`; must match the registered URI exactly. |
| `Authority` | The qrbound API that signs the tokens; defaults to `Qrbound:ApiBaseUrl`. |
| `Category` | A user category to ask for (e.g. `client`), if the application defines categories. |
| `Simulate` | Development only: a stand-in sign-in form instead of qrbound. |

The code is exchanged on the server with PKCE and the client secret, and the ID token's signature,
issuer, audience, lifetime and nonce are checked against qrbound's published keys
(`Qrbound.SignIn.Server`). The customer then has a cookie of its own (`.QrboundPos.Customer`),
separate from the staff cookie, which carries only their qrbound ID, name and email (no roles).

## Card payments through qrbound

At checkout the POS server creates a qrbound **QR payment request** for the order's total (tax
included) and the customer pays it on their phone with `QrboundPayLink`, adding a tip if the
business allows tips. On the register, "Card: customer scans" shows a QR code that opens the same page.
An order counts as paid only when qrbound says so: the POS asks qrbound after the customer pays, and
checks open requests every few seconds in the background (so hosted-page payments and closed phones
are caught too).

```json
"QrboundPos": {
  "Qrbound": {
    "ApiBaseUrl": "https://webapi.qrbound.com",
    "ServiceAccountEmail": "(user secrets)",
    "ServiceAccountPassword": "(user secrets)",
    "PaymentExpiresInMinutes": 60
  },
  "Payments": { "Simulate": false, "CheckIntervalSeconds": 20 }
}
```

- The service account is a qrbound user that is a member of each restaurant's qrbound business, with
  a role allowed to take payments. Set each restaurant's **qrbound business ID** in its Settings page.
- The card form runs in the server circuit, so calls to qrbound come from your server (no CORS setup).
- `Payments:Simulate` (Development only; startup refuses it elsewhere) replaces the card form with a
  Simulate payment button.
- A paid card order can't be cancelled in the POS: refund it in CardPointe (the reference is on the
  order), then cancel. Payments qrbound couldn't confirm show as **Check payment** with the invoice
  number to look up.

## Staff

Staff sign in at `/pos/signin` with a username and password (hashed with ASP.NET Core's
`PasswordHasher`, locked for 15 minutes after 5 wrong tries), in a cookie of the POS's own scheme. The
first Owner is created at `/pos/setup` with `QrboundPos:SetupKey`; setup closes once any account exists.

| Role | Can use |
| --- | --- |
| Staff | Orders, register, kitchen |
| Manager | Also menu, tables & QR codes, settings |
| Owner | Also staff accounts and new restaurants |

## Pages

| Route | |
| --- | --- |
| `/r/{slug}` | Takeaway menu (if the restaurant allows it) |
| `/r/{slug}/t/{code}` | A table's menu: what its printed QR code opens |
| `/r/{slug}/order/{token}` | The customer's order: progress and payment (its owner only) |
| `/pos/customer/signin`, `/callback`, `/signout` | Customer sign-in with qrbound |
| `/pos` | Restaurants |
| `/pos/{id}` | Today's orders, takings, payments |
| `/pos/{id}/register` | Register |
| `/pos/{id}/kitchen` | Kitchen screen |
| `/pos/{id}/menu`, `/tables`, `/settings` | Managers |
| `/pos/staff` | Owners |

Live updates between screens go through an in-process event (`PosEvents`); with several app servers,
add a backplane.
