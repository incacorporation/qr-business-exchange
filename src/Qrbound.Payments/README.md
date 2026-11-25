# Qrbound.Payments

Blazor client for qrbound card payments through CardPointe (Fiserv).

The card number is typed into CardPointe's hosted iFrame tokenizer, which returns a token.
Your app sends only that token, the expiry and the amount to your server, and the server
charges the card with its CardPointe credentials. Card numbers never reach your code, and
the gateway username and password never reach the browser.

## What's in it

| Type | What it is |
| --- | --- |
| `PaymentConfig` | Public settings from the server: merchant ID, tokenizer URL, currency, amount limits. |
| `CardPaymentRequest` | A charge: token, expiry (MMYY), cardholder name, amount in cents, invoice number. |
| `CardPaymentResult` | The outcome: approved or not, retref, auth code, a message safe to show. |
| `TokenizationResult` | What the hosted tokenizer posts back: token, expiry, validation error. |
| `QrboundPaymentsClient` | Calls `GET api/cardpointe/config` and `POST api/cardpointe/process`. |

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
| `QrboundPayLink` | The payer's page: the card form for the request's fixed amount, or why it can't be paid. `PaymentId`, `RequireEmail` (default false), `OnPaid`. |

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
