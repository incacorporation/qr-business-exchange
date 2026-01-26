# ??? Database Migration Quick Reference

## ? Quick Commands

```powershell
# Navigate to API project
cd "H:\Users\gmach\source\.net core\10.0\Passkey\QRBusinessExchange\src\QRBusinessExchange.API"

# Install/Update EF Tools
dotnet tool update --global dotnet-ef

# Apply migrations
dotnet ef database update --verbose

# Create Role migration
dotnet ef migrations add AddUserRoleColumn --project ..\QRBusinessExchange.Infrastructure\QRBusinessExchange.Infrastructure.csproj

# Apply new migration
dotnet ef database update --verbose

# Verify
dotnet ef migrations list
```

---

## ?? Automated Scripts

```powershell
# PowerShell (Recommended)
.\scripts\Run-Migrations.ps1

# Batch file (Windows)
.\scripts\Run-Migrations.bat
```

---

## ? Expected Results

```
20260124033740_InitialCreate (Applied)
[timestamp]_AddUserRoleColumn (Applied)
```

---

## ?? Test After Migration

```powershell
# Start API
cd src\QRBusinessExchange.API && dotnet run

# Check database (in new terminal)
curl https://localhost:5001/api/Health/database -k

# Register user
curl -X POST https://localhost:5001/api/Auth/register -H "Content-Type: application/json" -d '{"email":"test@qrbound.com","password":"Test@123456","firstName":"Test","lastName":"User"}' -k

# Login
curl -X POST https://localhost:5001/api/Auth/login -H "Content-Type: application/json" -d '{"email":"test@qrbound.com","password":"Test@123456"}' -k
```

---

## ?? Common Issues

| Error | Fix |
|-------|-----|
| "Cannot connect" | Check `incacorporation.net` accessible |
| "Login failed" | Verify credentials in connection string |
| "Build failed" | Run `dotnet build` and fix errors |
| "Already exists" | Tables exist - migrations already run |

---

## ?? What Gets Created

- ? 10 Tables (Users, Connections, Advertisements, etc.)
- ? Foreign keys and relationships
- ? Indexes for performance
- ? Role column in Users table
- ? Default values (timestamps, booleans)

---

## Connection String

```
Server=incacorporation.net
Database=www.qrbound.com
User Id=ssoAdministrator
Password=8^7lhy1F
```

---

**See full guide:** `docs\Database-Migrations-Guide.md`
