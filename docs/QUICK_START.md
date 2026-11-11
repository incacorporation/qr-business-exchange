# ?? Quick Start Guide - Blazor WebAssembly Hybrid

## What Was Done Automatically

? Created **QRBusinessExchange.Client** project (Blazor WebAssembly)
? Created all client services (ApiService, StateService, IndexedDBService)  
? Created MainLayout and basic pages (Home, Login)
? Updated **QRBusinessExchange.Web** to host WebAssembly client
? Created migration helper script

## 3-Step Quick Start

### Step 1: Run the Migration Script

Open PowerShell in the solution root directory and run:

```powershell
.\migrate-to-wasm.ps1
```

This will automatically copy:
- Static assets (CSS, JS, images)
- Pages (Dashboard, Profile, etc.)
- Components (Dashboard components, Map, Ads)
- Models

### Step 2: Add Client to Solution

**Option A - Visual Studio:**
1. Right-click solution ? Add ? Existing Project
2. Select `src\QRBusinessExchange.Client\QRBusinessExchange.Client.csproj`

**Option B - Command Line:**
```bash
dotnet sln add src\QRBusinessExchange.Client\QRBusinessExchange.Client.csproj
dotnet restore
```

### Step 3: Configure and Run

1. **Set Multiple Startup Projects:**
   - Right-click solution ? Properties
   - Select "Multiple startup projects"
   - Enable: QRBusinessExchange.API and QRBusinessExchange.Web

2. **Build:**
   ```bash
   dotnet build
   ```

3. **Run (F5 in Visual Studio or):**
   ```bash
   dotnet run --project src/QRBusinessExchange.API &
   dotnet run --project src/QRBusinessExchange.Web
   ```

4. **Test:**
   - Navigate to: https://localhost:7089
   - Login with your credentials
   - Verify state persists after page refresh

## That's It! ??

Your application is now running as a Blazor WebAssembly hybrid!

## Key URLs

- **Web App:** https://localhost:7089
- **API:** https://localhost:7059
- **API Swagger:** https://localhost:7059/swagger

## Verify Success

? Home page loads  
? Login works  
? State persists on refresh  
? No CORS errors in console  
? Navigation works

## Files Reference

- **Detailed Guide:** `MIGRATION_COMPLETE_SUMMARY.md`
- **Architecture Info:** `BLAZOR_WEBASSEMBLY_MIGRATION.md`
- **Helper Script:** `migrate-to-wasm.ps1`

## Need Help?

Check browser console (F12) for errors and refer to the troubleshooting section in `MIGRATION_COMPLETE_SUMMARY.md`.
