using System;
using System.IO;
using System.Text.Json;
using Npgsql;

namespace Ombi.DatabaseFix
{
    class DiagnosticTool
    {
        public static void DiagnoseDatabase()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("==============================================");
            Console.WriteLine("Ombi Database Diagnostic Tool");
            Console.WriteLine("==============================================");
            Console.ResetColor();
            Console.WriteLine();

            try
            {
                // Read database config
                var basePath = AppContext.BaseDirectory;
                var dbJsonPath = Path.Combine(basePath, "..", "..", "..", "..", "Ombi", "database.json");
                dbJsonPath = Path.GetFullPath(dbJsonPath);

                if (!File.Exists(dbJsonPath))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"database.json not found at: {dbJsonPath}");
                    Console.ResetColor();
                    return;
                }

                var json = File.ReadAllText(dbJsonPath);
                var doc = JsonDocument.Parse(json);

                // Check all three databases
                CheckDatabase("Ombi Database", doc.RootElement.GetProperty("OmbiDatabase").GetProperty("ConnectionString").GetString()!);
                Console.WriteLine();
                CheckDatabase("Settings Database", doc.RootElement.GetProperty("SettingsDatabase").GetProperty("ConnectionString").GetString()!);
                Console.WriteLine();
                CheckDatabase("External Database", doc.RootElement.GetProperty("ExternalDatabase").GetProperty("ConnectionString").GetString()!);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error: {ex.Message}");
                Console.ResetColor();
            }
        }

        static void CheckDatabase(string dbName, string connStr)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"Checking {dbName}...");
            Console.ResetColor();

            try
            {
                using var connection = new NpgsqlConnection(connStr);
                connection.Open();

                // List all schemas
                var schemaSql = @"
                    SELECT schema_name 
                    FROM information_schema.schemata 
                    WHERE schema_name NOT IN ('information_schema', 'pg_catalog', 'pg_toast')
                    ORDER BY schema_name;
                ";

                Console.WriteLine("  Schemas:");
                using (var cmd = new NpgsqlCommand(schemaSql, connection))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        Console.WriteLine($"    - {reader.GetString(0)}");
                    }
                }

                // List all tables grouped by schema
                var tableSql = @"
                    SELECT table_schema, table_name, 
                           (SELECT COUNT(*) FROM information_schema.columns c WHERE c.table_schema = t.table_schema AND c.table_name = t.table_name) as column_count
                    FROM information_schema.tables t
                    WHERE table_schema NOT IN ('information_schema', 'pg_catalog', 'pg_toast')
                    ORDER BY table_schema, table_name;
                ";

                Console.WriteLine("  Tables:");
                string? currentSchema = null;
                int tableCount = 0;
                
                using (var cmd = new NpgsqlCommand(tableSql, connection))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var schema = reader.GetString(0);
                        var table = reader.GetString(1);
                        var columns = reader.GetInt32(2);
                        
                        if (schema != currentSchema)
                        {
                            if (currentSchema != null)
                            {
                                Console.WriteLine();
                            }
                            Console.WriteLine($"    [{schema}]:");
                            currentSchema = schema;
                        }
                        
                        Console.WriteLine($"      - {table} ({columns} columns)");
                        tableCount++;
                    }
                }

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"  Total: {tableCount} tables");
                Console.ResetColor();

                // Check for specific important tables
                CheckForTable(connection, "GlobalSettings");
                CheckForTable(connection, "__EFMigrationsHistory");
                CheckForTable(connection, "AspNetUsers");
                CheckForTable(connection, "OmbiUser");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  Error: {ex.Message}");
                Console.ResetColor();
            }
        }

        static void CheckForTable(NpgsqlConnection connection, string tableName)
        {
            var sql = $@"
                SELECT table_schema, table_name
                FROM information_schema.tables
                WHERE LOWER(table_name) = LOWER('{tableName}')
                AND table_schema NOT IN ('information_schema', 'pg_catalog');
            ";

            using var cmd = new NpgsqlCommand(sql, connection);
            using var reader = cmd.ExecuteReader();
            
            if (reader.Read())
            {
                var schema = reader.GetString(0);
                var table = reader.GetString(1);
                Console.WriteLine($"  ✓ Found: {schema}.{table}");
            }
        }
    }
}
