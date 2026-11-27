# Production Deployment Guide for QRBusinessExchange API

## Overview
This guide covers the configuration required to deploy QRBusinessExchange.API to production on webapi.qrbound.com.

## Environment Variables Required

The following environment variables **must be set** on the production server before the application starts:

### 1. Database Configuration
```
Database__Password=<your_sql_server_password>
```
- Used for SQL Server authentication
- The connection string is in appsettings.json; only the password comes from the environment variable

### 2. JWT Configuration
```
JwtSettings__SecretKey=<your_very_long_secret_key>
```
- **CRITICAL**: Must be at least 32 characters (256+ bits)
- Used to sign and verify JWT tokens
- Should be unique and secure
- Generate with: `openssl rand -base64 32`

### 3. SMTP Configuration (Email Service)
```
EmailSettings__SmtpPassword=<your_smtp_password>
```
- Required for password reset emails and account verification
- The hostname, port, and username are in appsettings.json
- Currently configured for: `noreply@qrbound.com` on `qrbound.com:587`

## Network Configuration

### SSL/TLS Certificates
- Ensure valid SSL certificates are installed for:
  - `webapi.qrbound.com` (API)
  - `www.qrbound.com` (Web Portal)

### SMTP Access
- **Current Issue**: Your development IP is blocked by the SMTP server
- **Solution**: Ensure your deployment IP is whitelisted on `qrbound.com:587`
- Or configure a relay service that's not IP-restricted (e.g., SendGrid, AWS SES)

### WebAuthn Configuration
- `WebAuthn:RpId` is set to `webapi.qrbound.com`
- `WebAuthn:Origins` includes `https://www.qrbound.com`
- These must match your actual deployment URLs (already configured)

## Database Verification

Before deploying, verify:

1. **SQL Server Connection**: Test with the configured connection string + environment password
2. **Migrations**: Ensure the latest EF Core migrations have been applied
3. **User Permissions**: `ssoAdministrator` account must have full database permissions

Connection string (from appsettings.json):
```
Server=tcp:incacorporation.net,1433;Database=www.qrbound.com;User Id=ssoAdministrator;Encrypt=Mandatory;TrustServerCertificate=False;Connect Timeout=30;Persist Security Info=False
```

## Deployment Steps

1. **Build Release**
   ```powershell
   dotnet publish src/QRBusinessExchange.API --configuration Release --output ./publish
   ```

2. **Set Environment Variables** (on production server)
   - See "Environment Variables Required" section above

3. **Deploy to IIS**
   - Ensure .NET 10 runtime is installed
   - Configure Application Pool with appropriate permissions
   - Deploy published files to the IIS application folder

4. **Configure HTTPS**
   - Bind webapi.qrbound.com to HTTPS
   - Install SSL certificate for webapi.qrbound.com

5. **Verify Startup**
   - Check IIS logs at `%SystemRoot%\System32\LogFiles\HTTP\`
   - Check Event Viewer for application errors
   - Monitor stdout output if available

## Troubleshooting

### HTTP Error 500.30 - ASP.NET Core app failed to start
Common causes:
1. **Missing Environment Variables**: Check that all JwtSettings__SecretKey, Database__Password, etc. are set
2. **Database Connection Failed**: Verify SQL Server is accessible and credentials are correct
3. **Configuration Error**: Check if appsettings.json is valid JSON
4. **Missing Dependencies**: Ensure all NuGet packages are restored

**Solution**:
- Enable stdout logging on IIS to see startup errors
- Check IIS logs and Windows Event Viewer

### Email Service Errors
If password reset emails fail:
1. Verify `EmailSettings__SmtpPassword` environment variable is set
2. Check that your deployment IP is whitelisted on qrbound.com:587
3. Verify SMTP host, port, and username are correct
4. Check IIS logs for SMTP error details

## Configuration Reference

### appsettings.json (Production)
```json
{
  "Sso": {
	"Issuer": "https://webapi.qrbound.com",
	"PortalBaseUrl": "https://www.qrbound.com",
	"SigningKeyPath": "keys/sso-signing-key.pem"
  },
  "WebAuthn": {
	"RpId": "webapi.qrbound.com",
	"Origins": ["https://www.qrbound.com"]
  },
  "EmailSettings": {
	"SmtpHost": "qrbound.com",
	"SmtpPort": 587,
	"SmtpUser": "noreply@qrbound.com",
	"FromEmail": "noreply@qrbound.com"
  }
}
```

All sensitive values (passwords, secret keys) should come from environment variables, **never** hardcoded in config files.

## Recent Changes

### v1.0 (Current)
- ✅ Updated production configuration for webapi.qrbound.com and www.qrbound.com
- ✅ Added proper logging to EmailService for SMTP diagnostics
- ✅ Configured WebAuthn and SSO for production domains
- ⚠️ SMTP credentials must be provided via environment variable

## Support

If deployment fails:
1. Enable detailed logging
2. Check application startup logs
3. Verify all environment variables are set
4. Ensure database connectivity
5. Test SMTP connectivity from the server
