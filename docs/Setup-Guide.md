# QR Business Exchange - Setup Guide

## Prerequisites

- .NET 8.0 SDK
- SQL Server 2019 or later (or SQL Server Express)
- Node.js 18+ (for frontend build tools)
- Visual Studio 2022 or VS Code

## Installation Steps

### 1. Clone the Repository
```bash
git clone https://github.com/incacorporation/qr-business-exchange.git
cd qr-business-exchange
```

### 2. Configure Database Connection

Update the connection string in `src/QRBusinessExchange.API/appsettings.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=YOUR_SERVER;Database=www.qrbound.com;User Id=YOUR_USER;Password=YOUR_PASSWORD;TrustServerCertificate=True;"
  }
}
```

### 3. Configure Application Settings

Update `src/QRBusinessExchange.API/appsettings.json` with your keys:

```json
{
  "JwtSettings": {
    "SecretKey": "your-super-secret-key-min-32-characters",
    "Issuer": "QRBusinessExchange",
    "Audience": "QRBusinessExchange",
    "ExpirationMinutes": 15,
    "RefreshTokenExpirationDays": 7
  },
  "PaymentSettings": {
    "PayPalClientId": "your-paypal-client-id",
    "PayPalClientSecret": "your-paypal-secret",
    "StripeApiKey": "sk_test_your-stripe-key",
    "StripePublishableKey": "pk_test_your-stripe-key",
    "CloverApiKey": "your-clover-api-key",
    "CloverMerchantId": "your-clover-merchant-id"
  },
  "EmailSettings": {
    "SmtpHost": "smtp.gmail.com",
    "SmtpPort": 587,
    "SmtpUser": "your-email@gmail.com",
    "SmtpPassword": "your-app-password",
    "FromEmail": "noreply@qrbound.com",
    "FromName": "QR Business Exchange"
  },
  "OneSignalSettings": {
    "AppId": "your-onesignal-app-id",
    "ApiKey": "your-onesignal-api-key"
  }
}
```

### 4. Install EF Core Tools
```bash
dotnet tool install --global dotnet-ef
```

### 5. Create Database
```bash
cd src/QRBusinessExchange.API
dotnet ef database update
```

### 6. Build the Solution
```bash
cd ../..
dotnet build
```

### 7. Run the Applications

**Run API (Terminal 1):**
```bash
cd src/QRBusinessExchange.API
dotnet run
```
API will be available at: `http://localhost:5176`

**Run Web App (Terminal 2):**
```bash
cd src/QRBusinessExchange.Web
dotnet run
```
Web will be available at: `http://localhost:5100`

### 8. Access Swagger Documentation
Navigate to: `http://localhost:5176/swagger`

## Docker Setup

### Build and Run with Docker Compose
```bash
docker-compose up -d
```

Services will be available at:
- API: `http://localhost:5000`
- Web: `http://localhost:5100`
- SQL Server: `localhost:1433`

### Stop Docker Services
```bash
docker-compose down
```

## Development Tools

### Visual Studio 2022
1. Open `QRBusinessExchange.sln`
2. Set `QRBusinessExchange.API` as startup project
3. Press F5 to run

### VS Code
1. Install C# Dev Kit extension
2. Open folder in VS Code
3. Press F5 and select `.NET Core Launch`

## User Secrets (Development)

For sensitive data in development, use User Secrets:

```bash
cd src/QRBusinessExchange.API
dotnet user-secrets init
dotnet user-secrets set "JwtSettings:SecretKey" "your-secret-key"
dotnet user-secrets set "PaymentSettings:PayPalClientSecret" "your-secret"
```

## Testing

### Run Unit Tests
```bash
dotnet test tests/QRBusinessExchange.UnitTests
```

### Run Integration Tests
```bash
dotnet test tests/QRBusinessExchange.IntegrationTests
```

### Run All Tests
```bash
dotnet test
```

## Troubleshooting

### Database Connection Issues
- Verify SQL Server is running
- Check firewall settings
- Confirm connection string is correct
- Ensure user has proper permissions

### Migration Errors
```bash
# Remove last migration
dotnet ef migrations remove

# Create new migration
dotnet ef migrations add MigrationName

# Update database
dotnet ef database update
```

### Port Already in Use
Change ports in `launchSettings.json` files if default ports are taken.

## Next Steps

1. Configure payment gateway accounts (PayPal, Stripe, Clover)
2. Set up OneSignal for push notifications
3. Configure email SMTP settings
4. Review security settings
5. Set up SSL certificates for production
6. Configure CORS for your domain

## Support

For issues and questions:
- GitHub Issues: https://github.com/incacorporation/qr-business-exchange/issues
- Email: support@qrbound.com
