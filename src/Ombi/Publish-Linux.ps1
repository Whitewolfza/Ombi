# Ombi Linux Publish Script
# This script builds and publishes Ombi for Linux deployment

param(
    [ValidateSet('linux-x64', 'linux-arm64', 'linux-x64-standalone')]
    [string]$Target = 'linux-x64',
    
    [switch]$SkipAngularBuild,
    
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"

Write-Host "===============================================" -ForegroundColor Cyan
Write-Host "Ombi Linux Publish Script" -ForegroundColor Cyan
Write-Host "Target: $Target" -ForegroundColor Cyan
Write-Host "===============================================" -ForegroundColor Cyan

# Navigate to the script directory
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $scriptPath

# Step 1: Build Angular Application
if (-not $SkipAngularBuild) {
    Write-Host "`n[1/4] Building Angular application..." -ForegroundColor Yellow
    
    if (-not (Test-Path "ClientApp\node_modules")) {
        Write-Host "Installing npm packages..." -ForegroundColor Yellow
        Set-Location ClientApp
        npm install
        Set-Location ..
    }
    
    Set-Location ClientApp
    Write-Host "Running Angular build..." -ForegroundColor Yellow
    npm run build --configuration production
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Angular build failed!" -ForegroundColor Red
        exit 1
    }
    
    Set-Location ..
    Write-Host "Angular build completed successfully!" -ForegroundColor Green
} else {
    Write-Host "`n[1/4] Skipping Angular build..." -ForegroundColor Gray
}

# Step 2: Run Tests (optional)
if (-not $SkipTests) {
    Write-Host "`n[2/4] Running tests..." -ForegroundColor Yellow
    dotnet test ..\Ombi.sln --configuration Release --no-restore
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Tests failed! Continue anyway? (Y/N)" -ForegroundColor Red
        $continue = Read-Host
        if ($continue -ne 'Y' -and $continue -ne 'y') {
            exit 1
        }
    }
} else {
    Write-Host "`n[2/4] Skipping tests..." -ForegroundColor Gray
}

# Step 3: Publish .NET Application
Write-Host "`n[3/4] Publishing .NET application for $Target..." -ForegroundColor Yellow

$profilePath = "Properties\PublishProfiles\$Target.pubxml"

# Map target to profile file
$profileMap = @{
    'linux-x64' = 'Linux-x64.pubxml'
    'linux-arm64' = 'Linux-ARM64.pubxml'
    'linux-x64-standalone' = 'Linux-x64-SelfContained.pubxml'
}

$profileFile = $profileMap[$Target]
$profileFullPath = "Properties\PublishProfiles\$profileFile"

if (-not (Test-Path $profileFullPath)) {
    Write-Host "Publish profile not found: $profileFullPath" -ForegroundColor Red
    exit 1
}

Write-Host "Using publish profile: $profileFile" -ForegroundColor Yellow

dotnet publish Ombi.csproj /p:PublishProfile=$profileFile --configuration Release

if ($LASTEXITCODE -ne 0) {
    Write-Host ".NET publish failed!" -ForegroundColor Red
    exit 1
}

Write-Host ".NET publish completed successfully!" -ForegroundColor Green

# Step 4: Display output information
Write-Host "`n[4/4] Publish complete!" -ForegroundColor Green
Write-Host "===============================================" -ForegroundColor Cyan

$publishDir = "bin\Release\net8.0\publish\$Target"
$fullPath = Join-Path $scriptPath $publishDir

Write-Host "Output directory: $fullPath" -ForegroundColor White
Write-Host "`nTo deploy to Linux:" -ForegroundColor Yellow
Write-Host "1. Copy the contents of the publish folder to your Linux server" -ForegroundColor White
Write-Host "2. Ensure the Ombi executable has execute permissions:" -ForegroundColor White
Write-Host "   chmod +x Ombi" -ForegroundColor Gray
Write-Host "3. Run Ombi:" -ForegroundColor White
Write-Host "   ./Ombi --host http://*:5000" -ForegroundColor Gray

if ($Target -ne 'linux-x64-standalone') {
    Write-Host "`nNote: This is a framework-dependent deployment." -ForegroundColor Yellow
    Write-Host "Ensure .NET 8 Runtime is installed on the target Linux system." -ForegroundColor Yellow
    Write-Host "Install with: sudo apt install dotnet-runtime-8.0" -ForegroundColor Gray
}

Write-Host "`n===============================================" -ForegroundColor Cyan
