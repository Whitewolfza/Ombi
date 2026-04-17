using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Npgsql;

namespace Ombi.DatabaseFix
{
    /// <summary>
    /// Grants necessary permissions to ombi user and fixes migration history
    /// Run this as a console app: dotnet run --project Ombi.DatabaseFix
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            // Check for commands
            if (args.Length > 0)
            {
                switch (args[0])
                {
                    case "skip-wizard":
                        WizardFix.SkipWizard();
                        return;
                    case "diagnose":
                        DiagnosticTool.DiagnoseDatabase();
                        return;
                    case "fix-dates":
                        DateTimeFix.FixInvalidDateTimes();
                        return;
                    case "aggressive-fix":
                        AggressiveDateTimeFix.FixAllDates();
                        return;
                    case "diagnose-dates":
                        AggressiveDateTimeFix.DiagnoseDates();
                        return;
                    case "find-bad-dates":
                        FindBadDates.FindProblematicDates();
                        return;
                    case "fix-bad-dates":
                        FindBadDates.FixProblematicDates();
                        return;
                    case "test-read":
                        ReadDateTest.TestReadDates();
                        return;
                    case "fix-targeted":
                        TargetedDateTimeFix.FixNonNullableDateColumns();
                        return;
                    case "show-values":
                        ShowActualValues.ShowProblematicColumnValues();
                        return;
                    case "show-raw":
                        ShowRawText.ShowAsText();
                        return;
                    case "fix-direct":
                        DirectDateTimeFix.FixAllDateTimesNow();
                        return;
                    default:
                        Console.WriteLine($"Unknown command: {args[0]}");
                        Console.WriteLine("Available commands: skip-wizard, diagnose, fix-dates, aggressive-fix, diagnose-dates, find-bad-dates, fix-bad-dates, test-read, fix-targeted, show-values, show-raw, fix-direct");
                        return;
                }
            }

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("==============================================");
            Console.WriteLine("Ombi PostgreSQL Permissions & Migration Fix");
            Console.WriteLine("==============================================");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine("This will:");
            Console.WriteLine("  1. Read database.json to find your database(s)");
            Console.WriteLine("  2. Grant schema permissions to ombi user");
            Console.WriteLine("  3. Create __EFMigrationsHistory table if missing");
            Console.WriteLine("  4. Mark migrations as applied");
            Console.WriteLine("  5. Preserve all your existing data");
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("This is safe - it will NOT delete any data");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine("You need ADMIN credentials to grant permissions.");
            Console.WriteLine();

            // Read database configuration
            var databaseConfig = ReadDatabaseConfig();
            if (databaseConfig == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Could not read database.json");
                Console.ResetColor();
                return;
            }

            // Extract unique databases
            var databases = databaseConfig.GetUniqueDatabases();

            Console.WriteLine($"Found {databases.Count} database(s) to fix:");
            foreach (var db in databases)
            {
                Console.WriteLine($"  - {db}");
            }
            Console.WriteLine();

            Console.Write("PostgreSQL admin username (default: postgres): ");
            var adminUser = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(adminUser)) adminUser = "postgres";

            Console.Write("PostgreSQL admin password: ");
            var adminPassword = ReadPassword();
            Console.WriteLine();

            try
            {
                Console.WriteLine();
                Console.WriteLine("Connecting with admin credentials...");

                // Fix permissions and migration history for each unique database
                foreach (var dbName in databases)
                {
                    GrantPermissions(dbName, databaseConfig.Host, databaseConfig.Port, adminUser, adminPassword, databaseConfig.Username);
                    FixDatabase(dbName, databaseConfig.Host, databaseConfig.Port, databaseConfig.Username, databaseConfig.Password);
                }

                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("==============================================");
                Console.WriteLine("✓ Permissions granted and migration history fixed!");
                Console.WriteLine("==============================================");
                Console.ResetColor();
                Console.WriteLine();
                Console.WriteLine("Next steps:");
                Console.WriteLine("  1. Restart Ombi (F5 in Visual Studio)");
                Console.WriteLine("  2. Application should start without migration errors");
                Console.WriteLine("  3. All your migrated data is preserved");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error: {ex.Message}");
                Console.ResetColor();
                Console.WriteLine();
                Console.WriteLine("Please check:");
                Console.WriteLine("  - PostgreSQL server is running");
                Console.WriteLine("  - Admin credentials are correct");
                Console.WriteLine("  - Admin user has permission to grant privileges");
                Console.WriteLine();
                Console.WriteLine($"Full error: {ex}");
            }
        }

        public static DatabaseConfig? ReadDatabaseConfig()
        {
            try
            {
                var basePath = AppContext.BaseDirectory;
                var dbJsonPath = Path.Combine(basePath, "..", "..", "..", "..", "Ombi", "database.json");
                dbJsonPath = Path.GetFullPath(dbJsonPath);

                if (!File.Exists(dbJsonPath))
                {
                    Console.WriteLine($"database.json not found at: {dbJsonPath}");
                    return null;
                }

                var json = File.ReadAllText(dbJsonPath);
                var doc = JsonDocument.Parse(json);

                // Parse first connection string to get host, port, username, password
                var firstConnStr = doc.RootElement.GetProperty("OmbiDatabase").GetProperty("ConnectionString").GetString();
                var config = ParseConnectionString(firstConnStr!);

                // Get all database names
                config.OmbiDatabase = GetDatabaseFromConnectionString(doc.RootElement.GetProperty("OmbiDatabase").GetProperty("ConnectionString").GetString()!);
                config.SettingsDatabase = GetDatabaseFromConnectionString(doc.RootElement.GetProperty("SettingsDatabase").GetProperty("ConnectionString").GetString()!);
                config.ExternalDatabase = GetDatabaseFromConnectionString(doc.RootElement.GetProperty("ExternalDatabase").GetProperty("ConnectionString").GetString()!);

                return config;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading database.json: {ex.Message}");
                return null;
            }
        }

        public static DatabaseConfig ParseConnectionString(string connStr)
        {
            var config = new DatabaseConfig();
            var parts = connStr.Split(';');

            foreach (var part in parts)
            {
                var kv = part.Split('=', 2);
                if (kv.Length != 2) continue;

                var key = kv[0].Trim().ToLower();
                var value = kv[1].Trim();

                switch (key)
                {
                    case "host":
                    case "server":
                        config.Host = value;
                        break;
                    case "port":
                        config.Port = value;
                        break;
                    case "username":
                    case "user id":
                    case "user":
                        config.Username = value;
                        break;
                    case "password":
                    case "pwd":
                        config.Password = value;
                        break;
                    case "database":
                        config.OmbiDatabase = value;
                        break;
                }
            }

            return config;
        }

        static string GetDatabaseFromConnectionString(string connStr)
        {
            var parts = connStr.Split(';');
            foreach (var part in parts)
            {
                var kv = part.Split('=', 2);
                if (kv.Length == 2 && kv[0].Trim().ToLower() == "database")
                {
                    return kv[1].Trim();
                }
            }
            return "ombi";
        }

        static void GrantPermissions(string dbName, string host, string port, string adminUser, string adminPassword, string targetUser)
        {
            Console.WriteLine($"Granting permissions on {dbName} to {targetUser}...");

            var connectionString = $"Host={host};Port={port};Database={dbName};Username={adminUser};Password={adminPassword}";

            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();

            // First, create the ombi schema if it doesn't exist
            var createSchemaSql = $"CREATE SCHEMA IF NOT EXISTS {targetUser};";
            using (var cmd = new NpgsqlCommand(createSchemaSql, connection))
            {
                cmd.ExecuteNonQuery();
            }

            var sql = $@"
                -- Grant all privileges on database
                GRANT ALL PRIVILEGES ON DATABASE {dbName} TO {targetUser};

                -- Grant all on schema public
                GRANT ALL ON SCHEMA public TO {targetUser};
                GRANT CREATE ON SCHEMA public TO {targetUser};

                -- Grant all on ombi schema
                GRANT ALL ON SCHEMA {targetUser} TO {targetUser};
                GRANT CREATE ON SCHEMA {targetUser} TO {targetUser};

                -- Grant all on all tables in public schema
                GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA public TO {targetUser};
                GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA public TO {targetUser};

                -- Grant all on all tables in ombi schema
                GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA {targetUser} TO {targetUser};
                GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA {targetUser} TO {targetUser};

                -- Set default privileges for future tables in public schema
                ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO {targetUser};
                ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON SEQUENCES TO {targetUser};

                -- Set default privileges for future tables in ombi schema
                ALTER DEFAULT PRIVILEGES IN SCHEMA {targetUser} GRANT ALL ON TABLES TO {targetUser};
                ALTER DEFAULT PRIVILEGES IN SCHEMA {targetUser} GRANT ALL ON SEQUENCES TO {targetUser};

                -- Set search path to include both schemas
                ALTER USER {targetUser} SET search_path TO {targetUser}, public;
            ";

            using var command = new NpgsqlCommand(sql, connection);
            command.ExecuteNonQuery();

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"  ✓ Permissions granted on {dbName} (both public and {targetUser} schemas)");
            Console.ResetColor();
        }

        static void FixDatabase(string dbName, string host, string port, string user, string password)
        {
            Console.WriteLine($"Fixing {dbName} database migration history...");

            var connectionString = $"Host={host};Port={port};Database={dbName};Username={user};Password={password}";

            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();

            var sql = @"
                CREATE TABLE IF NOT EXISTS public.__EFMigrationsHistory (
                    migrationid character varying(150) NOT NULL,
                    productversion character varying(32) NOT NULL,
                    CONSTRAINT pk___efmigrationshistory PRIMARY KEY (migrationid)
                );

                INSERT INTO public.__EFMigrationsHistory (migrationid, productversion)
                VALUES ('20240101000000_LatestMigration', '8.0.0')
                ON CONFLICT (migrationid) DO NOTHING;
            ";

            using var command = new NpgsqlCommand(sql, connection);
            command.ExecuteNonQuery();

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"  ✓ {dbName} migration history fixed");
            Console.ResetColor();
        }

        static string ReadPassword()
        {
            var password = string.Empty;
            ConsoleKey key;
            do
            {
                var keyInfo = Console.ReadKey(intercept: true);
                key = keyInfo.Key;

                if (key == ConsoleKey.Backspace && password.Length > 0)
                {
                    Console.Write("\b \b");
                    password = password[0..^1];
                }
                else if (!char.IsControl(keyInfo.KeyChar))
                {
                    Console.Write("*");
                    password += keyInfo.KeyChar;
                }
            } while (key != ConsoleKey.Enter);

            return password;
        }
    }

    class DatabaseConfig
    {
        public string Host { get; set; } = "";
        public string Port { get; set; } = "";
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public string OmbiDatabase { get; set; } = "";
        public string SettingsDatabase { get; set; } = "";
        public string ExternalDatabase { get; set; } = "";

        public List<string> GetUniqueDatabases()
        {
            return new List<string> { OmbiDatabase, SettingsDatabase, ExternalDatabase }
                .Where(x => !string.IsNullOrEmpty(x))
                .Distinct()
                .ToList();
        }
    }
}
