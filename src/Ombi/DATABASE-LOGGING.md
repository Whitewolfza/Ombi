# Database Connection Logging

## Overview
Database connection settings are now logged on application startup to help with debugging and configuration verification.

## What Gets Logged

When Ombi starts up, it will display:
- Storage path location
- Database type for each database (Ombi, External, Settings)
- Sanitized connection strings (passwords are hidden)

## Example Output

### SQLite Configuration
```
==============================================
Database Configuration
==============================================
Storage Path: /etc/ombi

Ombi Database:
  Type: Sqlite
  Connection: Data Source=/etc/ombi/Ombi.db

External Database:
  Type: Sqlite
  Connection: Data Source=/etc/ombi/OmbiExternal.db

Settings Database:
  Type: Sqlite
  Connection: Data Source=/etc/ombi/OmbiSettings.db

==============================================
```

### PostgreSQL Configuration
```
==============================================
Database Configuration
==============================================
Storage Path: D:\Ombi

Ombi Database:
  Type: Postgres
  Connection: User ID=ombi; Password=****; Host=atmos.co.za; Port=5433; Database=ombi; Pooling=true

External Database:
  Type: Postgres
  Connection: User ID=ombi; Password=****; Host=atmos.co.za; Port=5433; Database=ombi_external; Pooling=true

Settings Database:
  Type: Postgres
  Connection: User ID=ombi; Password=****; Host=atmos.co.za; Port=5433; Database=ombi_settings; Pooling=true

==============================================
```

### MySQL Configuration
```
==============================================
Database Configuration
==============================================
Storage Path: /opt/ombi

Ombi Database:
  Type: MySQL
  Connection: Server=localhost; Port=3306; Database=ombi; User=ombiuser; Password=****

External Database:
  Type: MySQL
  Connection: Server=localhost; Port=3306; Database=ombi_external; User=ombiuser; Password=****

Settings Database:
  Type: MySQL
  Connection: Server=localhost; Port=3306; Database=ombi_settings; User=ombiuser; Password=****

==============================================
```

## Security Features

### Password Sanitization
Passwords are automatically masked with `****` for security:
- PostgreSQL: `Password` or `Pwd` parameters
- MySQL: `Password` or `Pwd` parameters
- SQLite: No passwords, shows file path only

### Error Handling
If there's an error reading the configuration, it will display:
```
Error logging database configuration: [error message]
```

## Implementation Details

### Files Modified
1. **`Ombi/Extensions/DatabaseExtensions.cs`**
   - Added `LogDatabaseConfiguration()` method
   - Added `LogDatabaseInfo()` helper method
   - Added `SanitizeConnectionString()` for password masking

2. **`Ombi/Program.cs`**
   - Added call to `DatabaseExtensions.LogDatabaseConfiguration()` after storage path is set
   - Logging happens before database contexts are created

### When Logging Occurs
The database configuration is logged:
1. ✅ After command-line arguments are parsed
2. ✅ After storage path is set
3. ✅ Before database contexts are created
4. ✅ Before any database migrations

This ensures you can verify the configuration before any database operations occur.

## Troubleshooting

### No Output Shown
If you don't see the database configuration:
- Check console output (it uses `Console.WriteLine`)
- Verify logging is enabled for the startup phase
- Check if there's an exception during configuration reading

### Connection String Shows "[Not configured]"
This means the connection string is empty or null. Check:
- `database.json` exists in the storage path
- The database configuration is properly set up
- The JSON is valid and properly formatted

### Connection String Shows "[Connection string parse error]"
This means there was an error parsing the connection string:
- Verify the connection string format is correct
- Check for special characters that might need escaping
- Review the database type matches the connection string format

## Related Files
- `/etc/ombi/database.json` (Linux) or `%APPDATA%\Ombi\database.json` (Windows)
- Configuration is loaded from the storage path specified via `--storage` argument

## Configuration File Example

The `database.json` file structure:
```json
{
  "OmbiDatabase": {
    "Type": "Postgres",
    "ConnectionString": "User ID=ombi;Password=secret;Host=localhost;Port=5432;Database=ombi"
  },
  "SettingsDatabase": {
    "Type": "Postgres",
    "ConnectionString": "User ID=ombi;Password=secret;Host=localhost;Port=5432;Database=ombi_settings"
  },
  "ExternalDatabase": {
    "Type": "Postgres",
    "ConnectionString": "User ID=ombi;Password=secret;Host=localhost;Port=5432;Database=ombi_external"
  }
}
```
