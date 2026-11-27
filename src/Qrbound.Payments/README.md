# Qrbound.Payments

Blazor client for qrbound card payments through CardPointe (Fiserv).

The card number is typed into CardPointe's hosted iFrame tokenizer, which returns a token.
Your app sends only that token, the expiry and the amount to your server, and the server
charges the card with its CardPointe credentials. Card numbers never reach your code, and
the gateway username and password never reach the browser.

## Installing (private GitHub Packages feed)

The package is published privately to the `incacorporation` GitHub Packages feed, next to
Qrbound.SignIn. Add a `nuget.config` next to your solution:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
    <add key="qrbound" value="https://nuget.pkg.github.com/incacorporation/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="nuget.org"><package pattern="*" /></packageSource>
    <packageSource key="qrbound"><package pattern="Qrbound.*" /></packageSource>
  </packageSourceMapping>
</configuration>
```

Give NuGet a GitHub token with `read:packages` (a classic personal access token, or in GitHub Actions
`${{ secrets.GITHUB_TOKEN }}` with `packages: read`). Store it in your user-level NuGet config, never in
the repository:

```bash
dotnet nuget add source https://nuget.pkg.github.com/incacorporation/index.json --name qrbound --username <github-user> --password <token>
```

Then add the package to your Blazor app (WebAssembly or Server, .NET 10):

```bash
dotnet add package Qrbound.Payments --version 0.2.0
```

## The server it talks to

The package is the browser side only. Charging a card needs CardPointe credentials, so it happens
on a server that exposes these endpoints (the qrbound API does; point `AddQrboundPayments` at it):

| Endpoint | Used by | Sign-in |
| --- | --- | --- |
| `GET api/cardpointe/config` | every component (tokenizer URL, currency, limits) | no |
| `POST api/cardpointe/process` | `QrboundCardPayment`, `ChargeAsync` | yes |
| `GET api/pay/{id}` | `QrboundPayLink`, `GetQrPaymentAsync` | no |
| `POST api/pay/{id}` | `QrboundPayLink`, `PayQrPaymentAsync` | no |
| `POST api/pay/{id}/quote` | `QuoteQrPaymentAsync` (lock a tip) | no |
| `POST api/pay/{id}/hosted` | `StartHostedPaymentAsync` (Fiserv's hosted page) | no |
| `POST api/pay/{id}/hosted/check` | `CheckHostedPaymentAsync` | no |

The server must also allow your app's origin in its CORS settings.

## What's in it

| Type | What it is |
| --- | --- |
| `PaymentConfig` | Public settings from the server: merchant ID, tokenizer URL, currency, amount limits. |
| `CardPaymentRequest` | A charge: token, expiry (MMYY), cardholder name, amount in cents, invoice number. |
| `CardPaymentResult` | The outcome: approved or not, retref, auth code, a message safe to show. |
| `TokenizationResult` | What the hosted tokenizer posts back: token, expiry, validation error. |
| `QrboundPaymentsClient` | Calls the endpoints above. |
| `QrboundCardPayment`, `CardPointeTokenizer`, `PaymentQrCode`, `QrboundPayLink` | The components, below. |

## Register

```csharp
builder.Services.AddQrboundPayments(new Uri("https://api.example.com/"))
    .AddHttpMessageHandler<YourBearerTokenHandler>(); // the process endpoint requires a signed-in user
```

## Components

Your host page must link its scoped-CSS bundle (`<link rel="stylesheet" href="YourApp.styles.css" />`),
which pulls in this library's styles.

### `QrboundCardPayment`: the whole payment

A full payment form: cardholder name, email, CardPointe's card fields, amount, currency, invoice
number and description, with Process Payment and Clear Form buttons. It checks every field when
Process Payment is pressed and shows what's missing under each one, charges the card and shows
the outcome. In test mode it shows the test card to use.

Pass an amount, invoice or description to fix it (shown read-only); leave it out and the payer
fills it in. With nothing passed, the payer types the amount and gets a generated invoice number:

```razor
<QrboundCardPayment OnCompleted="OnPaid" />
```

A fixed bill:

```razor
<QrboundCardPayment AmountCents="1250"
                    InvoiceNumber="INV-1001"
                    Description="Order 1001"
                    Title="Pay Example Coffee"
                    OnCompleted="OnPaid" />

@code {
    private void OnPaid(CardPaymentResult result)
    {
        // result.IsSuccess, result.TransactionId (CardPointe retref), result.ErrorMessage
    }
}
```

| Parameter | |
| --- | --- |
| `AmountCents` | Amount in cents, shown read-only. 0 (default) lets the payer enter it. Must be within the server's min/max. |
| `InvoiceNumber` | Sent to CardPointe as `orderid`, at most 19 characters, read-only. Empty generates `INV-yyMMdd-nnnnnn`, which the payer can change. |
| `Description` | Read-only when passed; otherwise the payer can describe the payment. |
| `Email` | Fills in the email; the payer can change it. |
| `RequireEmail` | Email is required (default `true`). |
| `Title`, `Subtitle`, `ButtonText` | Header and button text. |
| `ShowClearButton` | Show Clear Form, and New payment after an approval (default `true`). |
| `TokenizerCss`, `TokenizerHeight` | Styling and height of CardPointe's iframe. |
| `OnCompleted` | Raised once the gateway answers, approved or not. |

There is no CVV field: CardPointe's tokenizer can show one, but it posts back only the token and
expiry, so a CVV typed there would never reach the charge.

### `CardPointeTokenizer`: just the card fields

Hosts CardPointe's iFrame tokenizer (card number and expiry). Each time the payer leaves the
field, CardPointe posts a token, and `OnTokenized` receives a `TokenizationResult`. Only messages
from that iframe's window and origin are accepted. Expiry comes back as YYYYMM.

```razor
<CardPointeTokenizer TokenizerUrl="@config.TokenizerUrl" OnTokenized="r => card = r" />
```

## Charge a card yourself

```csharp
var config = await payments.GetConfigAsync();

var result = await payments.ChargeAsync(new CardPaymentRequest
{
    PaymentToken = tokenization.Token!,
    Expiry = "1228",             // MMYY; the tokenizer's YYYYMM (202812) converted
    CardholderName = "Pat Doe",
    AmountCents = 1250,          // $12.50
    Currency = config.Currency,
    InvoiceNumber = "INV-1001"
});
```

Amounts are whole cents (`long`), so $12.50 is `1250`.

## QR payments

A business shows a QR code for an amount; whoever scans it pays by card on their phone, no
account needed. The server charges the amount stored with the request, never one the payer sends.

1. Staff create a request: `POST api/businesses/{businessId}/qr-payments` (signed in, any member of the business).
2. The page shows `<PaymentQrCode Value="https://your.site/pay/{id}" />` and polls
   `GET api/businesses/{businessId}/qr-payments/{id}` until it's paid.
3. The payer's page hosts `<QrboundPayLink PaymentId="id" />`, which reads `GET api/pay/{id}` and
   pays with `POST api/pay/{id}` (both anonymous).

| Component | |
| --- | --- |
| `PaymentQrCode` | Draws a QR code as inline SVG (QRCoder, no JavaScript). `Value`, `Title`, `Size`. |
| `QrboundPayLink` | The payer's page: the card form for the request's fixed amount (or a `Quote`'s total), or why it can't be paid. `PaymentId`, `Quote`, `RequireEmail` (default false), `OnPaid`. |

### Tips

When the business takes tips (`QrPaymentInfo.TipsEnabled`, `TipPercentages`, `AllowCustomTip`,
`MaxTipPercent`), lock the customer's choice first and pay that quote:

```csharp
var quote = await payments.QuoteQrPaymentAsync(paymentId, tipCents);   // e.g. QrPaymentInfo.TipFor(amount, 18)
// then <QrboundPayLink PaymentId="paymentId" Quote="quote" />, or the hosted page below
```

The server checks the tip against the business's rules and charges exactly the quote's total. A
newer quote replaces an older one, and a quote times out after 15 minutes.

### Fiserv's Hosted Payment Page

When the server has a CardPointe Hosted Payment Page (`QrPaymentInfo.HostedPageAvailable`), the
customer can pay there instead, submitting the payment to Fiserv themselves:

```csharp
var start = await payments.StartHostedPaymentAsync(paymentId, quote.QuoteId);
// show <a href="@start.Url" target="_blank">: the page opens with the total and invoice filled in
var info = await payments.CheckHostedPaymentAsync(paymentId);           // poll until info.Status is Paid
```

The server marks the bill paid only after CardPointe reports the payment (from the hosted page's
webhook, or when asked). Paying less than the bill there leaves it in `NeedsReview`; anything above
the bill counts as tip.

Safeguards on the server (`QrPaymentService`):

- A request is charged at most once: it's claimed (Open → Processing) before CardPointe is called,
  so a second payer gets "already in progress".
- After 5 declined cards the request locks (`Locked`); staff start a new one.
- It expires (5 minutes to 24 hours, default 30).
- If CardPointe doesn't answer in time, the request goes to `NeedsReview`: nobody can pay it until
  staff look the invoice number up in CardPointe and mark it paid or not charged.

States: `Open`, `Processing`, `Paid`, `Cancelled`, `Locked`, `NeedsReview`, and `Expired` (an Open
request past its expiry).

`QrboundCardPayment` also takes a `ChargeHandler`, so the same form can send the card anywhere,
which is how `QrboundPayLink` pays a request.
