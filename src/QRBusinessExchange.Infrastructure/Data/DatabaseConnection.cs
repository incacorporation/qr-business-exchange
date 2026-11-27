using Microsoft.Data.SqlClient;

namespace QRBusinessExchange.Infrastructure.Data;

/// <summary>
/// Builds the SQL Server connection string from configuration and enforces the security rules:
/// the password comes only from a secret store (user-secrets, environment variable or key vault),
/// never from source code or appsettings.json, and the connection must always be encrypted.
/// </summary>
public static class DatabaseConnection
{
    /// <summary>Configuration key for the non-secret connection string (server, database, user, TLS options).</summary>
    public const string ConnectionStringKey = "DefaultConnection";

    /// <summary>Configuration key for the database password. Environment variable: <c>Database__Password</c>.</summary>
    public const string PasswordKey = "Database:Password";

    public static SqlConnectionStringBuilder Build(string? connectionString, string? password, string applicationName)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{ConnectionStringKey}' is not configured.");
        }

        var builder = new SqlConnectionStringBuilder(connectionString);

        if (!string.IsNullOrEmpty(password))
        {
            builder.Password = password;
        }

        if (!builder.IntegratedSecurity && string.IsNullOrEmpty(builder.Password))
        {
            throw new InvalidOperationException(
                $"The database password is not configured. Set '{PasswordKey}' with " +
                "'dotnet user-secrets' (development) or the 'Database__Password' environment variable (servers).");
        }

        // Never allow an unencrypted connection: Optional sends credentials and data in clear text.
        if (builder.Encrypt == SqlConnectionEncryptOption.Optional)
        {
            throw new InvalidOperationException(
                "The database connection must be encrypted: use Encrypt=Mandatory or Encrypt=Strict.");
        }

        // Keep the password out of SqlConnection.ConnectionString once the connection is open.
        builder.PersistSecurityInfo = false;

        // Identifies this app in the server's session and audit views.
        if (string.IsNullOrEmpty(builder.ApplicationName) || builder.ApplicationName == "Core Microsoft SqlClient Data Provider")
        {
            builder.ApplicationName = applicationName;
        }

        return builder;
    }
}
