@echo off
REM Database Migration Script for QR Business Exchange
REM .NET 10 Project - Entity Framework Core Migrations

echo ===================================================
echo QR Business Exchange - Database Migration
echo ===================================================
echo.

REM Navigate to API project
cd src\QRBusinessExchange.API
if errorlevel 1 (
    echo Error: Cannot find API project directory
    echo Make sure you're running this from the solution root
    pause
    exit /b 1
)

echo Current directory: %CD%
echo.

REM Step 1: Check/Install EF Core Tools
echo === Step 1: Checking EF Core Tools ===
dotnet ef --version
if errorlevel 1 (
    echo Installing EF Core tools...
    dotnet tool install --global dotnet-ef
)
echo.

REM Step 2: Build Project
echo === Step 2: Building Project ===
dotnet build --configuration Release
if errorlevel 1 (
    echo Build failed. Please fix errors first.
    pause
    exit /b 1
)
echo Build succeeded!
echo.

REM Step 3: List Migrations
echo === Step 3: Current Migrations ===
dotnet ef migrations list
echo.

REM Step 4: Apply Migrations
echo === Step 4: Applying Migrations ===
echo This will create/update database schema...
echo.
set /p confirm="Apply migrations to database? (Y/N): "
if /i "%confirm%"=="Y" (
    echo Applying migrations...
    dotnet ef database update --verbose
    if errorlevel 1 (
        echo.
        echo Migration failed!
        echo Common issues:
        echo   - Database server not accessible
        echo   - Wrong connection string
        echo   - Firewall blocking port 1433
        pause
        exit /b 1
    )
    echo Migrations applied successfully!
) else (
    echo Skipped applying migrations.
)
echo.

REM Step 5: Create Role Migration
echo === Step 5: Create Role Column Migration ===
set /p createRole="Create migration for User Role column? (Y/N): "
if /i "%createRole%"=="Y" (
    echo Creating AddUserRoleColumn migration...
    dotnet ef migrations add AddUserRoleColumn --project ..\QRBusinessExchange.Infrastructure\QRBusinessExchange.Infrastructure.csproj
    if errorlevel 0 (
        echo Migration created!
        echo.
        set /p applyNew="Apply the new migration now? (Y/N): "
        if /i "!applyNew!"=="Y" (
            dotnet ef database update --verbose
            echo Role column added!
        )
    )
)
echo.

REM Step 6: Final Status
echo === Step 6: Final Migration Status ===
dotnet ef migrations list
echo.

REM Navigate back
cd ..\..

echo ===================================================
echo Migration Script Complete!
echo ===================================================
echo.
echo Next steps:
echo   1. Run: cd src\QRBusinessExchange.API ^&^& dotnet run
echo   2. Test: curl https://localhost:5001/api/Health/database -k
echo   3. Register user via /api/Auth/register
echo.
echo Database: Server=incacorporation.net;Database=www.qrbound.com
echo.
pause
