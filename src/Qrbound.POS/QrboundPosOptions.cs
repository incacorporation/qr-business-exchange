namespace Qrbound.POS;

/// <summary>
/// Settings for the POS, bound from the <c>QrboundPos</c> configuration section.
/// </summary>
/// <example>
/// <code>
/// "ConnectionStrings": { "QrboundPos": "Server=.;Database=RestaurantPos;Trusted_Connection=True;TrustServerCertificate=True" },
/// "QrboundPos": {
///   "ConnectionStringName": "QrboundPos",
///   "Qrbound": { "ApiBaseUrl": "https://webapi.qrbound.com" }
/// }
/// </code>
/// </example>
public sealed class QrboundPosOptions
{
    public const string SectionName = "QrboundPos";

    /// <summary>
    /// Which entry of <c>ConnectionStrings</c> holds the SQL Server database (default <c>QrboundPos</c>).
    /// Point it at an existing entry to put the POS tables (schema <c>pos</c>) in a database you already have.
    /// </summary>
    public string ConnectionStringName { get; set; } = "QrboundPos";

    /// <summary>The connection string itself; when set it wins over <see cref="ConnectionStringName"/>.</summary>
    public string? ConnectionString { get; set; }

    /// <summary>Create or upgrade the POS tables when the app starts (default true).</summary>
    public bool ApplyMigrationsOnStartup { get; set; } = true;

    /// <summary>SQL command timeout in seconds; null keeps SQL Server's default (30).</summary>
    public int? CommandTimeoutSeconds { get; set; }

    /// <summary>
    /// The address customers reach the app at, used in printed table QR codes, e.g.
    /// <c>https://order.example.com/</c>. Null uses the address the staff member is browsing.
    /// </summary>
    public string? PublicBaseUrl { get; set; }

    /// <summary>
    /// Needed once, to create the first Owner account on /pos/setup. Keep it in user secrets or an
    /// environment variable. Setup is closed once any staff account exists.
    /// </summary>
    public string? SetupKey { get; set; }

    public QrboundConnectionOptions Qrbound { get; set; } = new();

    public PaymentOptions Payments { get; set; } = new();

    public CustomerSignInOptions CustomerSignIn { get; set; } = new();
}

/// <summary>
/// Customers sign in with their qrbound account before they see the menu, order or pay. The POS is a
/// confidential qrbound SSO application: register it in the qrbound portal (Businesses → your business →
/// Applications → New application, Confidential) with the redirect URI
/// <c>https://your-pos-host/pos/customer/callback</c>.
/// </summary>
public sealed class CustomerSignInOptions
{
    /// <summary>Customers must be signed in to qrbound to order and pay (default true).</summary>
    public bool Required { get; set; } = true;

    /// <summary>The client_id of the POS's qrbound SSO application.</summary>
    public string? ClientId { get; set; }

    /// <summary>Its client secret. User secrets, environment variables or a vault only.</summary>
    public string? ClientSecret { get; set; }

    /// <summary>
    /// The exact redirect URI registered for the application. Null uses
    /// <see cref="QrboundPosOptions.PublicBaseUrl"/> + <c>pos/customer/callback</c>.
    /// </summary>
    public string? RedirectUri { get; set; }

    /// <summary>qrbound's sign-in pages (default https://www.qrbound.com).</summary>
    public string PortalBaseUrl { get; set; } = "https://www.qrbound.com";

    /// <summary>
    /// The qrbound API that issues and signs the tokens. Null uses <see cref="QrboundConnectionOptions.ApiBaseUrl"/>.
    /// </summary>
    public string? Authority { get; set; }

    /// <summary>
    /// A user category of the application to ask for (e.g. <c>client</c>), when the application defines
    /// categories in the qrbound portal. Null asks for none.
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Development only: a stand-in sign-in form instead of qrbound, for trying the POS without a
    /// registered application. Refused outside the Development environment.
    /// </summary>
    public bool Simulate { get; set; }
}

/// <summary>How the POS reaches the qrbound API that charges cards.</summary>
public sealed class QrboundConnectionOptions
{
    /// <summary>Base URL of the qrbound API, e.g. <c>https://webapi.qrbound.com</c>.</summary>
    public string ApiBaseUrl { get; set; } = "https://webapi.qrbound.com";

    /// <summary>
    /// The qrbound account the POS signs in as to create payment requests. It must be a member of every
    /// restaurant's qrbound business, with a role allowed to take payments. Keep it in secrets.
    /// </summary>
    public string? ServiceAccountEmail { get; set; }

    /// <summary>Its password. User secrets, environment variables or a vault only, never appsettings.json.</summary>
    public string? ServiceAccountPassword { get; set; }

    /// <summary>How long a customer has to pay an order's payment request, in minutes (5 to 1440).</summary>
    public int PaymentExpiresInMinutes { get; set; } = 60;
}

public sealed class PaymentOptions
{
    /// <summary>
    /// Development only: take "payments" without qrbound, through a Simulate payment button. Refused
    /// outside the Development environment.
    /// </summary>
    public bool Simulate { get; set; }

    /// <summary>How often open payment requests are checked with qrbound, in seconds (default 20).</summary>
    public int CheckIntervalSeconds { get; set; } = 20;
}
