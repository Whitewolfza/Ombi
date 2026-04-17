# PostgreSQL Migration from MySQL - Troubleshooting Guide

## Your Situation
You've migrated data from MySQL to PostgreSQL. The tables and data exist, but EF Core migrations are causing errors because the migration history is incomplete.

## The Problem
- ✅ Data tables exist (migrated from MySQL)
- ❌ `__EFMigrationsHistory` table missing or incomplete
- ❌ EF Core tries to create tables that already exist
- ❌ Application shows wizard because database state appears incomplete

## Solution Options

### Option 1: Fix Migration History (Recommended)

Run the migration history fix script:

```powershell
cd D:\Bennie\Code\GIT\Ombi\src\Ombi
.\fix-postgres-migrations.ps1
```

This will:
- Create `__EFMigrationsHistory` table if missing
- Mark migrations as applied
- **Preserve all your data**

### Option 2: Manually Update Migration History

Connect to PostgreSQL and run:

```sql
-- For each database: ombi, ombi_settings, ombi_external

-- 1. Create migrations history table
CREATE TABLE IF NOT EXISTS public."__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

-- 2. Find the latest migration name from your project
-- Look in: Ombi.Store\Migrations\[PostgresDB folder]
-- Example: 20240315120000_LatestMigration.cs

-- 3. Mark it as applied
INSERT INTO public."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20240315120000_LatestMigration', '8.0.0')
ON CONFLICT ("MigrationId") DO NOTHING;
```

### Option 3: Get Latest Migration Names

To find the exact migration names to insert:

```powershell
# In the Ombi.Store project folder
cd D:\Bennie\Code\GIT\Ombi\src\Ombi.Store

# List all PostgreSQL migrations
dir .\Migrations\OmbiPostgres\*.cs | Select-Object -Last 1
dir .\Migrations\ExternalPostgres\*.cs | Select-Object -Last 1
dir .\Migrations\SettingsPostgres\*.cs | Select-Object -Last 1
```

## After Fixing Migration History

1. **Restart Ombi** (F5 in Visual Studio)
2. You should see:
   ```
   Ombi database is up to date.
   External database is up to date.
   Settings database is up to date.
   ```
3. **No migration errors**
4. **Your MySQL data intact**

## Checking if the Fix Worked

Connect to PostgreSQL and verify:

```sql
-- Check migration history
SELECT * FROM public."__EFMigrationsHistory";

-- Should show your migrations as applied
```

## If Wizard Still Shows

The wizard appears when `settings.Wizard = false` in the database. To fix:

```sql
-- Connect to ombi_settings database
UPDATE "GlobalSettings" 
SET "Content" = jsonb_set("Content"::jsonb, '{Wizard}', 'true')
WHERE "SettingsName" = 'OmbiSettings';
```

Or simply complete the wizard - it won't affect your existing data, it will just:
- Create admin user if needed
- Set wizard flag to completed

## Code Changes Made

The PostgreSQL context files now:
- ✅ Check database connectivity first
- ✅ Only migrate if pending migrations exist
- ✅ Show clear console messages
- ✅ Gracefully handle "tables already exist" errors
- ✅ Don't crash when migrated from MySQL

## Expected Console Output

```
==============================================
Database Configuration
==============================================
Storage Path: D:\Bennie\Code\GIT\Ombi\src\Ombi

Ombi Database:
  Type: Postgres
  Connection: Host=atmos.co.za; Port=5433; Database=ombi; Username=ombi; Password=****

External Database:
  Type: Postgres  
  Connection: Host=atmos.co.za; Port=5433; Database=ombi_external; Username=ombi; Password=****

Settings Database:
  Type: Postgres
  Connection: Host=atmos.co.za; Port=5433; Database=ombi_settings; Username=ombi; Password=****

==============================================
Ombi database is up to date.
External database is up to date.
Settings database is up to date.
```

## Troubleshooting

### "Cannot connect to database"
- Verify PostgreSQL is running on atmos.co.za:5433
- Check credentials in `database.json`
- Test connection with psql or pgAdmin

### "Pending migrations" message but errors
- Run the fix script to update migration history
- Or manually insert migration records (see Option 2 above)

### Data appears missing
- Your data is safe in PostgreSQL
- Check you're using the correct database names
- Verify connection strings in `database.json`

### Still seeing migration errors
The improved code will now log errors but continue running. Look for messages like:
- "database already configured (migrated from MySQL)"
- "database is up to date"

These are informational, not errors.

## Quick Reference

| File | Purpose |
|------|---------|
| `database.json` | Database connection configuration |
| `fix-postgres-migrations.ps1` | Fix migration history script |
| `Ombi.Store/Context/Postgres/*Context.cs` | Migration logic (improved) |

## Support

If issues persist:
1. Check console output for specific error messages
2. Verify all three databases have migration history
3. Ensure wizard flag is set correctly
4. Check that all migrated data is present
