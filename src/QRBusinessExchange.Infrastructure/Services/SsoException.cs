namespace QRBusinessExchange.Infrastructure.Services;

/// <summary>
/// Error raised by the SSO / business services. <see cref="Error"/> is a short,
/// machine-readable code (OAuth style, e.g. "invalid_client"), and
/// <see cref="StatusCode"/> is the HTTP status the API should return.
/// </summary>
public class SsoException : Exception
{
    public SsoException(string error, string description, int statusCode = 400)
        : base(description)
    {
        Error = error;
        StatusCode = statusCode;
    }

    public string Error { get; }

    public int StatusCode { get; }

    public static SsoException NotFound(string what) => new("not_found", $"{what} not found", 404);

    public static SsoException Forbidden(string description = "You do not have permission to manage this business") =>
        new("forbidden", description, 403);
}
