# PostgreSQL Database Reset Script for Ombi
# This script drops and recreates the Ombi databases

$host = "atmos.co.za"
$port = "5433"
$adminUser = "postgres"  # Change to your PostgreSQL admin user
$ombiPassword = "Ombi#159357"  # Ombi user password from appsettings.json

Write-Host "=============================================="-ForegroundColor Cyan
Write-Host "Ombi PostgreSQL Database Reset" -ForegroundColor Cyan
Write-Host "=============================================="  -ForegroundColor Cyan
Write-Host ""
Write-Host "This will:" -ForegroundColor Yellow
Write-Host "  1. Drop databases: ombi, ombi_settings, ombi_external" -ForegroundColor Yellow
Write-Host "  2. Recreate them fresh" -ForegroundColor Yellow
Write-Host ""
Write-Host "WARNING: This will delete all existing Ombi data!" -ForegroundColor Red
Write-Host ""
$confirm = Read-Host "Type 'YES' to continue"

if ($confirm -ne "YES") {
    Write-Host "Cancelled." -ForegroundColor Yellow
    exit
}

Write-Host ""
Write-Host "Enter PostgreSQL admin password:" -ForegroundColor Yellow
$adminPassword = Read-Host -AsSecureString
$adminPasswordPlain = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto([System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($adminPassword))

Write-Host ""
Write-Host "Connecting to PostgreSQL..." -ForegroundColor Yellow

$env:PGPASSWORD = $adminPasswordPlain

try {
    Write-Host "Dropping existing databases..." -ForegroundColor Yellow

    psql -h $host -p $port -U $adminUser -d postgres -c "DROP DATABASE IF EXISTS ombi;"
    psql -h $host -p $port -U $adminUser -d postgres -c "DROP DATABASE IF EXISTS ombi_settings;"
    psql -h $host -p $port -U $adminUser -d postgres -c "DROP DATABASE IF EXISTS ombi_external;"

    Write-Host "Creating fresh databases..." -ForegroundColor Green

    psql -h $host -p $port -U $adminUser -d postgres -c "CREATE DATABASE ombi OWNER ombi;"
    psql -h $host -p $port -U $adminUser -d postgres -c "CREATE DATABASE ombi_settings OWNER ombi;"
    psql -h $host -p $port -U $adminUser -d postgres -c "CREATE DATABASE ombi_external OWNER ombi;"

    Write-Host ""
    Write-Host "=============================================="  -ForegroundColor Green
    Write-Host "✓ Databases reset successfully!" -ForegroundColor Green
    Write-Host "=============================================="  -ForegroundColor Green
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Cyan
    Write-Host "  1. Restart Ombi (F5 in Visual Studio)" -ForegroundColor White
    Write-Host "  2. Complete the wizard to create admin user" -ForegroundColor White
    Write-Host "  3. Database migrations will run automatically" -ForegroundColor White
}
catch {
    Write-Host ""
    Write-Host "Error: $_" -ForegroundColor Red
    Write-Host ""
    Write-Host "Make sure psql is installed and in your PATH" -ForegroundColor Yellow
    Write-Host "Install from: https://www.postgresql.org/download/windows/" -ForegroundColor Yellow
}
finally {
    Remove-Item Env:\PGPASSWORD
}
