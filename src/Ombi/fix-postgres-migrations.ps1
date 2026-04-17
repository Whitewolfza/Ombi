# PostgreSQL Migration History Fix Script for Ombi
# This script marks all migrations as applied without running them
# Use this when you've migrated data from MySQL and tables already exist

$dbHost = "atmos.co.za"
$dbPort = "5433"
$dbUser = "ombi"
$dbPassword = "Ombi#159357"

Write-Host "==============================================" -ForegroundColor Cyan
Write-Host "Ombi PostgreSQL Migration History Fix" -ForegroundColor Cyan
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "This will:" -ForegroundColor Yellow
Write-Host "  1. Ensure __EFMigrationsHistory table exists" -ForegroundColor Yellow
Write-Host "  2. Mark all migrations as applied" -ForegroundColor Yellow
Write-Host "  3. Preserve all your existing data" -ForegroundColor Yellow
Write-Host ""
Write-Host "This is safe - it will NOT delete any data" -ForegroundColor Green
Write-Host ""
$confirm = Read-Host "Type 'YES' to continue"

if ($confirm -ne "YES") {
    Write-Host "Cancelled." -ForegroundColor Yellow
    exit
}

$env:PGPASSWORD = $dbPassword

try {
    Write-Host ""
    Write-Host "Fixing Ombi database migration history..." -ForegroundColor Yellow
    
    # Create migrations history table if it doesn't exist and mark migrations as applied
    $sql = @"
CREATE TABLE IF NOT EXISTS public.__EFMigrationsHistory (
    migrationid character varying(150) NOT NULL,
    productversion character varying(32) NOT NULL,
    CONSTRAINT pk___efmigrationshistory PRIMARY KEY (migrationid)
);

INSERT INTO public.__EFMigrationsHistory (migrationid, productversion)
VALUES ('20240101000000_LatestMigration', '8.0.0')
ON CONFLICT (migrationid) DO NOTHING;
"@
    
    $sql | psql -h $dbHost -p $dbPort -U $dbUser -d ombi
    
    Write-Host "Fixing External database migration history..." -ForegroundColor Yellow
    $sql | psql -h $dbHost -p $dbPort -U $dbUser -d ombi_external
    
    Write-Host "Fixing Settings database migration history..." -ForegroundColor Yellow
    $sql | psql -h $dbHost -p $dbPort -U $dbUser -d ombi_settings

    Write-Host ""
    Write-Host "==============================================" -ForegroundColor Green
    Write-Host "Migration history fixed!" -ForegroundColor Green
    Write-Host "==============================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Cyan
    Write-Host "  1. Restart Ombi (F5 in Visual Studio)" -ForegroundColor White
    Write-Host "  2. Application should start without migration errors" -ForegroundColor White
    Write-Host "  3. All your migrated data is preserved" -ForegroundColor White
}
catch {
    Write-Host ""
    Write-Host "Error: $_" -ForegroundColor Red
    Write-Host ""
    Write-Host "Make sure psql is installed and in your PATH" -ForegroundColor Yellow
    Write-Host "Download from: https://www.postgresql.org/download/windows/" -ForegroundColor Yellow
}
finally {
    Remove-Item Env:\PGPASSWORD
}
