# Database Migrations Guide - .NET 10

## Overview

This guide walks you through running Entity Framework Core migrations for the QR Business Exchange .NET 10 application.

---

## Quick Start

### **Option 1: PowerShell Script (Recommended)**

```powershell
cd "H:\Users\gmach\source\.net core\10.0\Passkey\QRBusinessExchange"
.\scripts\Run-Migrations.ps1
```

### **Option 2: Batch Script (Windows)**

```cmd
cd "H:\Users\gmach\source\.net core\10.0\Passkey\QRBusinessExchange"
scripts\Run-Migrations.bat
```

### **Option 3: Manual Commands**

```powershell
cd "H:\Users\gmach\source\.net core\10.0\Passkey\QRBusinessExchange\src\QRBusinessExchange.API"

# 1. Install/Update EF Core tools
dotnet tool update --global dotnet-ef

# 2. Build project
dotnet build

# 3. Apply existing migrations
dotnet ef database update --verbose

# 4. Create Role migration
dotnet ef migrations add AddUserRoleColumn --project ..\QRBusinessExchange.Infrastructure\QRBusinessExchange.Infrastructure.csproj

# 5. Apply Role migration
dotnet ef database update --verbose

# 6. Verify
dotnet ef migrations list
```

---

## What These Migrations Do

### **InitialCreate Migration** (Already Exists)

Creates the complete database schema:

**Tables Created:**
- ? **Users** - User accounts (email, password, profile)
- ? **Connections** - User connections/networking
- ? **Advertisements** - User advertisements
- ? **Subscriptions** - User subscription plans
- ? **Payments** - Payment transactions
- ? **QRCodes** - Generated QR codes for users
- ? **UserLocations** - User geographic locations
- ? **EmailVerifications** - Email verification tokens
- ? **PasskeyCredentials** - Passkey authentication data
- ? **UsageAnalytics** - Usage tracking data

**Features:**
- Primary keys (GUIDs with auto-generation)
- Foreign key relationships
- Indexes for performance
- Default values (timestamps, booleans)
- Constraints (email uniqueness, etc.)

---

### **AddUserRoleColumn Migration** (New - Needs to be Created)

Adds role-based access control:

**Changes:**
- Adds `Role` column to Users table
- Type: `nvarchar(50)`
- Default value: `"User"`
- Required (NOT NULL)

**Supported Roles:**
- `User` - Regular users (default)
- `Admin` - Administrator access
- `SuperAdmin` - Full system access

---

## Prerequisites

### **1. EF Core Tools Installed**

```powershell
# Check if installed
dotnet ef --version

# Install if needed
dotnet tool install --global dotnet-ef

# Or update existing
dotnet tool update --global dotnet-ef
```

**Expected Version**: 10.x.x (for .NET 10)

---

### **2. Database Server Accessible**

**Your Connection String:**
```
Server=incacorporation.net
Database=www.qrbound.com
User Id=ssoAdministrator
Password=8^7lhy1F
```

**Test Connection:**
```powershell
# Using sqlcmd (if installed)
sqlcmd -S incacorporation.net -U ssoAdministrator -P "8^7lhy1F" -Q "SELECT @@VERSION"
```

**Firewall Requirements:**
- Outbound access to `incacorporation.net`
- Port `1433` (default SQL Server port)
- SQL Server authentication enabled

---

### **3. Project Builds Successfully**

```powershell
cd src\QRBusinessExchange.API
dotnet build
```

Should complete with `Build succeeded.`

---

## Step-by-Step Migration Process

### **Step 1: Navigate to API Project**

```powershell
cd "H:\Users\gmach\source\.net core\10.0\Passkey\QRBusinessExchange\src\QRBusinessExchange.API"
```

---

### **Step 2: List Current Migrations**

```powershell
dotnet ef migrations list
```

**Expected Output:**
```
Build succeeded.
20260124033740_InitialCreate (Pending)
```

If `(Pending)` - not applied yet
If `(Applied)` - already in database

---

### **Step 3: Apply Initial Migration**

```powershell
dotnet ef database update --verbose
```

**What Happens:**
1. Connects to SQL Server
2. Creates database if it doesn't exist
3. Creates `__EFMigrationsHistory` table (tracks applied migrations)
4. Runs `InitialCreate` migration
5. Creates all tables, indexes, constraints

**Expected Output:**
```
Build succeeded.
Applying migration '20260124033740_InitialCreate'.
Executing DbCommand [Parameters=[], CommandType='Text']
CREATE TABLE [Users] (
    [UserId] uniqueidentifier NOT NULL DEFAULT (NEWID()),
    [Email] nvarchar(255) NOT NULL,
    ...
)
Done.
```

**Success Indicators:**
- No red error messages
- "Done." at the end
- Exit code 0

---

### **Step 4: Create Role Migration**

```powershell
dotnet ef migrations add AddUserRoleColumn --project ..\QRBusinessExchange.Infrastructure\QRBusinessExchange.Infrastructure.csproj
```

**What Happens:**
1. Analyzes current DbContext
2. Compares with last migration snapshot
3. Detects new `Role` property in User entity
4. Generates migration code

**Files Created:**
```
src\QRBusinessExchange.Infrastructure\Data\Migrations\
  ?? [timestamp]_AddUserRoleColumn.cs
  ?? [timestamp]_AddUserRoleColumn.Designer.cs
  ?? ApplicationDbContextModelSnapshot.cs (updated)
```

**Expected Output:**
```
Build succeeded.
Done. To undo this action, use 'dotnet ef migrations remove'
```

---

### **Step 5: Apply Role Migration**

```powershell
dotnet ef database update --verbose
```

**What Happens:**
1. Detects new pending migration
2. Executes SQL to add Role column
3. Updates migration history

**SQL Executed:**
```sql
ALTER TABLE [Users] 
ADD [Role] nvarchar(50) NOT NULL DEFAULT 'User';
```

**Expected Output:**
```
Applying migration '[timestamp]_AddUserRoleColumn'.
Done.
```

---

### **Step 6: Verify Migrations**

```powershell
dotnet ef migrations list
```

**Expected Output:**
```
20260124033740_InitialCreate (Applied)
[timestamp]_AddUserRoleColumn (Applied)
```

All migrations should show `(Applied)`

---

## Verification Tests

### **Test 1: Database Health Check**

```powershell
# Start API
cd src\QRBusinessExchange.API
dotnet run

# In another terminal
curl https://localhost:5001/api/Health/database -k
```

**Expected Response:**
```json
{
  "status": "healthy",
  "database": "connected",
  "userCount": 0,
  "databaseName": "www.qrbound.com",
  "connectionState": "Open"
}
```

---

### **Test 2: Check Tables Exist**

```powershell
curl https://localhost:5001/api/Health/users -k
```

**Expected Response:**
```json
{
  "count": 0,
  "users": []
}
```

---

### **Test 3: Register User**

```powershell
curl -X POST "https://localhost:5001/api/Auth/register" `
  -H "Content-Type: application/json" `
  -d '{
    "email": "test@qrbound.com",
    "password": "Test@123456",
    "firstName": "Test",
    "lastName": "User"
  }' `
  -k
```

**Expected Response:**
```json
{
  "accessToken": "eyJhbGc...",
  "user": {
    "email": "test@qrbound.com",
    "role": "User",
    "isEmailVerified": false
  }
}
```

---

### **Test 4: Login**

```powershell
curl -X POST "https://localhost:5001/api/Auth/login" `
  -H "Content-Type: application/json" `
  -d '{
    "email": "test@qrbound.com",
    "password": "Test@123456"
  }' `
  -k
```

**Should return JWT tokens** ?

---

## Troubleshooting

### **Error: "A network-related or instance-specific error"**

**Cause:** Cannot connect to SQL Server

**Solutions:**
1. Check server is online: `ping incacorporation.net`
2. Check firewall allows port 1433
3. Verify VPN is connected (if required)
4. Test connection with SQL client

---

### **Error: "Login failed for user 'ssoAdministrator'"**

**Cause:** Wrong credentials or permissions

**Solutions:**
1. Verify username: `ssoAdministrator`
2. Verify password: `8^7lhy1F`
3. Check SQL Server allows SQL Authentication
4. Verify user has CREATE DATABASE permissions

---

### **Error: "Cannot open database 'www.qrbound.com'"**

**Cause:** Database doesn't exist and user lacks CREATE permissions

**Solutions:**
1. Create database manually in SQL Server
2. Grant CREATE DATABASE permission
3. Or change connection string to existing database

---

### **Error: "There is already an object named 'Users'"**

**Cause:** Tables already exist from previous migration

**Solutions:**

**Option A: Drop and recreate (DELETES ALL DATA!)**
```powershell
dotnet ef database drop
dotnet ef database update
```

**Option B: Generate new migration from existing**
```powershell
dotnet ef migrations add InitialFromExisting --no-build
```

---

### **Error: "Build failed"**

**Cause:** Project has compilation errors

**Solution:**
```powershell
dotnet build
# Fix any errors shown
# Then retry migration
```

---

### **Error: "No DbContext was found"**

**Cause:** Wrong directory

**Solution:**
```powershell
# Make sure you're in the API project
cd src\QRBusinessExchange.API
```

---

## Advanced Operations

### **Remove Last Migration** (if mistake)

```powershell
dotnet ef migrations remove
```

?? **Only if not yet applied to database!**

---

### **Revert to Specific Migration**

```powershell
# Revert to InitialCreate
dotnet ef database update InitialCreate

# Revert all migrations
dotnet ef database update 0
```

?? **This removes data!**

---

### **Generate SQL Script** (without applying)

```powershell
# For all migrations
dotnet ef migrations script -o migration.sql

# For specific migration
dotnet ef migrations script InitialCreate AddUserRoleColumn -o role-migration.sql
```

Use for manual database updates or review.

---

### **Check Pending Migrations**

```powershell
dotnet ef migrations has-pending-model-changes
```

Returns exit code 0 if no pending changes.

---

## Migration Best Practices

### **? DO:**
- Always backup database before migrations
- Test migrations on development database first
- Use `--verbose` flag to see detailed SQL
- Commit migration files to source control
- Review generated SQL before applying

### **? DON'T:**
- Run migrations on production without testing
- Manually edit generated migration files (unless needed)
- Delete migrations that are already applied
- Share database with multiple developers during migration development

---

## Database Schema Summary

After migrations, your database will have:

| Table | Purpose | Key Columns |
|-------|---------|-------------|
| **Users** | User accounts | Email, PasswordHash, Role |
| **Connections** | User networking | InitiatorUserId, RecipientUserId, Status |
| **Advertisements** | User ads | UserId, AdContent, CostPerDay |
| **Subscriptions** | Subscription plans | UserId, PlanType, Price |
| **Payments** | Payment records | UserId, Amount, PaymentMethod |
| **QRCodes** | Generated QR codes | UserId, QRCodeData, ScansCount |
| **UserLocations** | User locations | UserId, Latitude, Longitude |
| **EmailVerifications** | Email tokens | UserId, VerificationToken |
| **PasskeyCredentials** | Passkey auth | UserId, CredentialId |
| **UsageAnalytics** | Usage tracking | UserId, Action, Timestamp |

---

## Post-Migration Checklist

After successful migrations:

- [ ] All migrations show `(Applied)`
- [ ] `/api/Health/database` returns "healthy"
- [ ] Can register new user
- [ ] New user has `"role": "User"` by default
- [ ] Can create admin user with `"role": "Admin"`
- [ ] Login works and returns JWT with role claim
- [ ] Database has 0 users initially (check with `/api/Health/users`)

---

## Next Steps

1. ? **Migrations Complete** - Database ready
2. ?? **Configure SMTP** - For email features (optional)
3. ?? **Register Users** - Create accounts via `/api/Auth/register`
4. ?? **Create Admin** - Use `/api/Auth/register-admin`
5. ?? **Test APIs** - All endpoints now functional
6. ?? **Deploy** - Ready for production

---

## Support

**Migration Files Location:**
```
src\QRBusinessExchange.Infrastructure\Data\Migrations\
```

**DbContext Location:**
```
src\QRBusinessExchange.Infrastructure\Data\ApplicationDbContext.cs
```

**Connection String Location:**
```
src\QRBusinessExchange.API\Program.cs (line 18)
```

**Migration Scripts:**
- `scripts\Run-Migrations.ps1` (PowerShell)
- `scripts\Run-Migrations.bat` (Windows Batch)

---

## Summary

Migrations create your database schema for the .NET 10 QR Business Exchange application. They handle:

- ? Database creation
- ? Table creation (10 tables)
- ? Relationships and constraints
- ? Indexes for performance
- ? Default values
- ? Role-based access control

**Ready to run migrations!** Use one of the scripts or manual commands above. ???
