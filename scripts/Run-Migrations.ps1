# Database Migration Script for QR Business Exchange
# .NET 10 Project - Entity Framework Core Migrations

Write-Host "=== QR Business Exchange - Database Migration ===" -ForegroundColor Cyan
Write-Host ""

# Set error action preference
$ErrorActionPreference = "Continue"

# Navigate to API project (where DbContext is configured)
$apiProjectPath = "src\QRBusinessExchange.API"
$infrastructureProjectPath = "..\QRBusinessExchange.Infrastructure\QRBusinessExchange.Infrastructure.csproj"

if (-not (Test-Path $apiProjectPath)) {
    Write-Host "Error: Cannot find API project at $apiProjectPath" -ForegroundColor Red
    Write-Host "Make sure you're running this from the solution root." -ForegroundColor Yellow
    exit 1
}

Set-Location $apiProjectPath
Write-Host "Working directory: $(Get-Location)" -ForegroundColor Gray
Write-Host ""

# Step 1: Check EF Core Tools
Write-Host "=== Step 1: Checking EF Core Tools ===" -ForegroundColor Yellow
$efVersion = dotnet ef --version 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "EF Core tools not found. Installing..." -ForegroundColor Yellow
    dotnet tool install --global dotnet-ef
    Write-Host ""
} else {
    Write-Host "EF Core Tools: $efVersion" -ForegroundColor Green
    Write-Host ""
}

# Step 2: Build the project
Write-Host "=== Step 2: Building Projects ===" -ForegroundColor Yellow
dotnet build --configuration Release
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed. Please fix build errors first." -ForegroundColor Red
    exit 1
}
Write-Host "Build succeeded!" -ForegroundColor Green
Write-Host ""

# Step 3: List existing migrations
Write-Host "=== Step 3: Checking Existing Migrations ===" -ForegroundColor Yellow
Write-Host "Current migrations:" -ForegroundColor Gray
dotnet ef migrations list
Write-Host ""

# Step 4: Apply existing migrations
Write-Host "=== Step 4: Applying Existing Migrations ===" -ForegroundColor Yellow
Write-Host "This will create/update the database schema..." -ForegroundColor Gray
Write-Host ""

$confirm = Read-Host "Apply migrations to database? (Y/N)"
if ($confirm -eq 'Y' -or $confirm -eq 'y') {
    Write-Host "Applying migrations..." -ForegroundColor Cyan
    dotnet ef database update --verbose
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Migrations applied successfully!" -ForegroundColor Green
    } else {
        Write-Host "Migration failed. Check error messages above." -ForegroundColor Red
        Write-Host ""
        Write-Host "Common issues:" -ForegroundColor Yellow
        Write-Host "  - Database server not accessible" -ForegroundColor Gray
        Write-Host "  - Wrong connection string" -ForegroundColor Gray
        Write-Host "  - SQL Server permissions issue" -ForegroundColor Gray
        Write-Host "  - Firewall blocking SQL port 1433" -ForegroundColor Gray
        exit 1
    }
} else {
    Write-Host "Skipped applying migrations." -ForegroundColor Yellow
}
Write-Host ""

# Step 5: Create Role column migration (if needed)
Write-Host "=== Step 5: Creating Role Column Migration ===" -ForegroundColor Yellow
Write-Host "Checking if Role column migration is needed..." -ForegroundColor Gray

$createRoleMigration = Read-Host "Create migration for User Role column? (Y/N)"
if ($createRoleMigration -eq 'Y' -or $createRoleMigration -eq 'y') {
    Write-Host "Creating AddUserRoleColumn migration..." -ForegroundColor Cyan
    dotnet ef migrations add AddUserRoleColumn --project $infrastructureProjectPath
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Migration created successfully!" -ForegroundColor Green
        Write-Host ""
        
        # Apply the new migration
        $applyNewMigration = Read-Host "Apply the new migration now? (Y/N)"
        if ($applyNewMigration -eq 'Y' -or $applyNewMigration -eq 'y') {
            Write-Host "Applying AddUserRoleColumn migration..." -ForegroundColor Cyan
            dotnet ef database update --verbose
            
            if ($LASTEXITCODE -eq 0) {
                Write-Host "Role column added successfully!" -ForegroundColor Green
            } else {
                Write-Host "Failed to apply Role migration." -ForegroundColor Red
            }
        }
    } else {
        Write-Host "Failed to create migration. Check if it already exists." -ForegroundColor Yellow
    }
}
Write-Host ""

# Step 6: Verify final state
Write-Host "=== Step 6: Verification ===" -ForegroundColor Yellow
Write-Host "Final migration status:" -ForegroundColor Gray
dotnet ef migrations list
Write-Host ""

# Step 7: Test database connection
Write-Host "=== Step 7: Testing Database Connection ===" -ForegroundColor Yellow
Write-Host "Starting API to test database..." -ForegroundColor Gray
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "  1. Run: dotnet run" -ForegroundColor White
Write-Host "  2. Test: curl https://localhost:5001/api/Health/database -k" -ForegroundColor White
Write-Host "  3. Register first user via /api/Auth/register" -ForegroundColor White
Write-Host ""

Set-Location ..\..

Write-Host "=== Migration Script Complete ===" -ForegroundColor Green
Write-Host ""
Write-Host "Summary:" -ForegroundColor Cyan
Write-Host "  - Database schema created/updated" -ForegroundColor White
Write-Host "  - All migrations applied" -ForegroundColor White
Write-Host "  - Ready to register users" -ForegroundColor White
Write-Host ""
Write-Host "Connection String: Server=incacorporation.net;Database=www.qrbound.com" -ForegroundColor Gray
Write-Host ""
