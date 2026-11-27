namespace Qrbound.SignIn;

/// <summary>How the authorization code is turned into tokens.</summary>
public enum QrboundTokenExchange
{
    /// <summary>
    /// The browser calls qrbound's token endpoint itself with the PKCE verifier (public client, no secret).
    /// </summary>
    Direct,

    /// <summary>
    /// The browser posts the code and verifier to your own backend (see the Qrbound.SignIn.Server package),
    /// which exchanges it — optionally with a client secret — and starts your app's own session.
    /// </summary>
    Backend
}

/// <summary>
/// Settings for <c>AddQrboundSignIn(...)</c>.
/// </summary>
public class QrboundOptions
{
    /// <summary>Base URL of the qrbound API, e.g. <c>https://webapi.qrbound.com</c>.</summary>
    public string Authority { get; set; } = "https://webapi.qrbound.com";

    /// <summary>
    /// Base URL of the qrbound sign-in portal, used for the "continue in browser" redirect flow,
    /// e.g. <c>https://www.qrbound.com</c>.
    /// </summary>
    public string PortalBaseUrl { get; set; } = "https://www.qrbound.com";

    /// <summary>The client_id of the SSO application registered for your app in qrbound.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// An exact redirect URI registered for the application. The QR flow binds the code to it and the
    /// redirect flow sends the browser back to it (render <c>&lt;QrboundCallback /&gt;</c> there).
    /// </summary>
    public string RedirectUri { get; set; } = string.Empty;

    /// <summary>
    /// Expected <c>iss</c> of tokens. Defaults to <see cref="Authority"/>.
    /// </summary>
    public string? Issuer { get; set; }

    /// <summary>Who exchanges the code. Defaults to <see cref="QrboundTokenExchange.Direct"/>.</summary>
    public QrboundTokenExchange TokenExchange { get; set; } = QrboundTokenExchange.Direct;

    /// <summary>
    /// Path (or absolute URL) of your backend's exchange endpoint when
    /// <see cref="TokenExchange"/> is <see cref="QrboundTokenExchange.Backend"/>.
    /// </summary>
    public string BackendExchangePath { get; set; } = "/qrbound/exchange";

    /// <summary>Poll interval used when the server does not suggest one.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>Allowed clock difference when checking token lifetimes.</summary>
    public TimeSpan ClockSkew { get; set; } = TimeSpan.FromMinutes(1);

    internal string AuthorityTrimmed => Authority.TrimEnd('/');
    internal string ExpectedIssuer => (Issuer ?? Authority).TrimEnd('/');

    /// <summary>Throws when a required setting is missing.</summary>
    public void Validate()
    {
        if (!Uri.TryCreate(Authority, UriKind.Absolute, out _))
            throw new InvalidOperationException("Qrbound: Authority must be an absolute URL.");
        if (!Uri.TryCreate(PortalBaseUrl, UriKind.Absolute, out _))
            throw new InvalidOperationException("Qrbound: PortalBaseUrl must be an absolute URL.");
        if (string.IsNullOrWhiteSpace(ClientId))
            throw new InvalidOperationException("Qrbound: ClientId is required.");
        if (!Uri.TryCreate(RedirectUri, UriKind.Absolute, out _))
            throw new InvalidOperationException("Qrbound: RedirectUri must be an absolute URL registered for the application.");
    }
}
