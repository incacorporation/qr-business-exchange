namespace QRBusinessExchange.Infrastructure.Configuration;

/// <summary>
/// Configuration settings for SMTP email service
/// </summary>
public class EmailSettings
{
    /// <summary>
    /// Gets or sets the SMTP server host address
    /// </summary>
    /// <remarks>
    /// Examples: "smtp.gmail.com", "smtp.office365.com", "smtp.sendgrid.net"
    /// </remarks>
    public string SmtpHost { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the SMTP server port number
    /// </summary>
    /// <remarks>
    /// Common ports:
    /// - 25: Standard SMTP (not recommended for client use)
    /// - 587: SMTP with STARTTLS (recommended)
    /// - 465: SMTP with SSL/TLS (legacy)
    /// </remarks>
    public int SmtpPort { get; set; } = 587;

    /// <summary>
    /// Gets or sets the SMTP username for authentication
    /// </summary>
    /// <remarks>
    /// Typically the email address or account name for the SMTP service
    /// </remarks>
    public string SmtpUser { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the SMTP password for authentication
    /// </summary>
    /// <remarks>
    /// Should be stored securely in configuration (e.g., user secrets, environment variables, or Azure Key Vault).
    /// For Gmail, use an App Password rather than the account password.
    /// </remarks>
    public string SmtpPassword { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the email address to use as the sender
    /// </summary>
    /// <remarks>
    /// Example: "noreply@qrbusinessexchange.com"
    /// </remarks>
    public string FromEmail { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name for the sender
    /// </summary>
    /// <remarks>
    /// Example: "QR Business Exchange"
    /// </remarks>
    public string FromName { get; set; } = string.Empty;
}
