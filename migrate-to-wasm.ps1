# Blazor WebAssembly Migration Helper Script
# This script helps copy files from Blazor Server to WebAssembly Client

$webProject = "H:\Users\gmach\source\.net core\10.0\Passkey\QRBusinessExchange\src\QRBusinessExchange.Web"
$clientProject = "H:\Users\gmach\source\.net core\10.0\Passkey\QRBusinessExchange\src\QRBusinessExchange.Client"

Write-Host "?? Blazor WebAssembly Migration Helper" -ForegroundColor Cyan
Write-Host "=======================================" -ForegroundColor Cyan
Write-Host ""

# Check if Client project exists
if (-not (Test-Path $clientProject)) {
    Write-Host "? Client project not found at: $clientProject" -ForegroundColor Red
    exit 1
}

# Step 1: Copy static assets
Write-Host "?? Step 1: Copying static assets..." -ForegroundColor Yellow

$assetsPath = "$webProject\wwwroot"
$clientWwwroot = "$clientProject\wwwroot"

# Create directories if they don't exist
New-Item -ItemType Directory -Force -Path "$clientWwwroot\css" | Out-Null
New-Item -ItemType Directory -Force -Path "$clientWwwroot\js" | Out-Null

# Copy CSS
if (Test-Path "$assetsPath\css") {
    Copy-Item -Path "$assetsPath\css\*" -Destination "$clientWwwroot\css" -Recurse -Force
    Write-Host "   ? CSS files copied" -ForegroundColor Green
}

# Copy JS files
if (Test-Path "$assetsPath\js") {
    Copy-Item -Path "$assetsPath\js\*" -Destination "$clientWwwroot\js" -Recurse -Force
    Write-Host "   ? JavaScript files copied" -ForegroundColor Green
}

# Copy favicon
if (Test-Path "$assetsPath\favicon.png") {
    Copy-Item -Path "$assetsPath\favicon.png" -Destination "$clientWwwroot\" -Force
    Write-Host "   ? Favicon copied" -ForegroundColor Green
}

# Copy bootstrap if exists
if (Test-Path "$assetsPath\bootstrap") {
    New-Item -ItemType Directory -Force -Path "$clientWwwroot\bootstrap" | Out-Null
    Copy-Item -Path "$assetsPath\bootstrap\*" -Destination "$clientWwwroot\bootstrap" -Recurse -Force
    Write-Host "   ? Bootstrap files copied" -ForegroundColor Green
}

# Step 2: List pages to copy
Write-Host ""
Write-Host "?? Step 2: Pages that need manual copying:" -ForegroundColor Yellow

$pagesPath = "$webProject\Components\Pages"
if (Test-Path $pagesPath) {
    $pages = Get-ChildItem -Path $pagesPath -Filter "*.razor" | Where-Object { $_.Name -ne "Login.razor" }
    
    New-Item -ItemType Directory -Force -Path "$clientProject\Pages" | Out-Null
    
    foreach ($page in $pages) {
        Write-Host "   ?? $($page.Name)" -ForegroundColor Cyan
        
        # Read content and update namespaces
        $content = Get-Content $page.FullName -Raw
        
        # Replace namespaces
        $content = $content -replace '@using QRBusinessExchange.Web.Services', '@using QRBusinessExchange.Client.Services'
        $content = $content -replace '@using QRBusinessExchange.Web.Models', '@using QRBusinessExchange.Client.Models'
        
        # Remove Blazor Server specific directives
        $content = $content -replace '@rendermode InteractiveServer\s*', ''
        $content = $content -replace '@inject RenderModeService.*?\n', ''
        $content = $content -replace '@implements IAsyncDisposable\s*', ''
        
        # Save to client project
        $destPath = "$clientProject\Pages\$($page.Name)"
        Set-Content -Path $destPath -Value $content
        Write-Host "      ? Copied and updated" -ForegroundColor Green
    }
}

# Step 3: Copy components
Write-Host ""
Write-Host "?? Step 3: Copying components..." -ForegroundColor Yellow

$componentsSource = "$webProject\Components"
$componentsToCheck = @("Dashboard", "Map", "Ads")

foreach ($componentFolder in $componentsToCheck) {
    $sourcePath = "$componentsSource\$componentFolder"
    if (Test-Path $sourcePath) {
        $destPath = "$clientProject\Components\$componentFolder"
        New-Item -ItemType Directory -Force -Path $destPath | Out-Null
        
        $components = Get-ChildItem -Path $sourcePath -Filter "*.razor"
        foreach ($component in $components) {
            $content = Get-Content $component.FullName -Raw
            
            # Update namespaces
            $content = $content -replace '@using QRBusinessExchange.Web.Services', '@using QRBusinessExchange.Client.Services'
            $content = $content -replace '@using QRBusinessExchange.Web.Models', '@using QRBusinessExchange.Client.Models'
            $content = $content -replace '@rendermode InteractiveServer\s*', ''
            
            $componentDest = "$destPath\$($component.Name)"
            Set-Content -Path $componentDest -Value $content
            Write-Host "   ? $componentFolder/$($component.Name)" -ForegroundColor Green
        }
    }
}

# Step 4: Copy Models
Write-Host ""
Write-Host "?? Step 4: Copying models..." -ForegroundColor Yellow

$modelsSource = "$webProject\Models"
if (Test-Path $modelsSource) {
    $modelsDest = "$clientProject\Models"
    New-Item -ItemType Directory -Force -Path $modelsDest | Out-Null
    
    Copy-Item -Path "$modelsSource\*" -Destination $modelsDest -Recurse -Force
    Write-Host "   ? Models copied" -ForegroundColor Green
}

Write-Host ""
Write-Host "? Migration steps completed!" -ForegroundColor Green
Write-Host ""
Write-Host "?? Next steps:" -ForegroundColor Cyan
Write-Host "   1. Add Client project to solution (if not already done)"
Write-Host "   2. Build the solution: dotnet build"
Write-Host "   3. Set multiple startup projects (API and Web)"
Write-Host "   4. Run and test: https://localhost:7089"
Write-Host ""
Write-Host "?? See MIGRATION_COMPLETE_SUMMARY.md for detailed instructions" -ForegroundColor Yellow
