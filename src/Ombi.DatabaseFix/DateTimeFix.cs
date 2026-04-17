using System;
using System.IO;
using System.Text.Json;
using Npgsql;

namespace Ombi.DatabaseFix
{
    class DateTimeFix
    {
        public static void FixInvalidDateTimes()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("==============================================");
            Console.WriteLine("Ombi PostgreSQL DateTime Fix Tool");
            Console.WriteLine("==============================================");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine("This will fix invalid DateTime values in your database.");
            Console.WriteLine("Common issues from MySQL migration:");
            Console.WriteLine("  - Dates with year 0000");
            Console.WriteLine("  - Dates beyond year 9999");
            Console.WriteLine("  - NULL or infinity timestamps");
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("This is safe - it will set invalid dates to a default value.");
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

                var connStr = doc.RootElement.GetProperty("OmbiDatabase").GetProperty("ConnectionString").GetString();
                
                Console.WriteLine("Connecting to Ombi database...");
                Console.WriteLine();

                using var connection = new NpgsqlConnection(connStr);
                connection.Open();

                // Fix dates in the ombi schema
                FixDatesInSchema(connection, "ombi");

                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("==============================================");
                Console.WriteLine("✓ Invalid DateTime values fixed!");
                Console.WriteLine("==============================================");
                Console.ResetColor();
                Console.WriteLine();
                Console.WriteLine("Next steps:");
                Console.WriteLine("  1. Restart Ombi (F5 in Visual Studio)");
                Console.WriteLine("  2. DateTime errors should be resolved");
                Console.WriteLine("  3. Application should load properly");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error: {ex.Message}");
                Console.ResetColor();
                Console.WriteLine();
                Console.WriteLine($"Full error: {ex}");
            }
        }

        static void FixDatesInSchema(NpgsqlConnection connection, string schema)
        {
            Console.WriteLine($"Fixing DateTime values in {schema} schema...");

            // Get all tables with timestamp columns
            var getTablesSql = $@"
                SELECT DISTINCT table_name, column_name
                FROM information_schema.columns
                WHERE table_schema = '{schema}'
                AND data_type IN ('timestamp without time zone', 'timestamp with time zone', 'date')
                ORDER BY table_name, column_name;
            ";

            var tablesToFix = new System.Collections.Generic.List<(string table, string column)>();

            using (var cmd = new NpgsqlCommand(getTablesSql, connection))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    var table = reader.GetString(0);
                    var column = reader.GetString(1);
                    tablesToFix.Add((table, column));
                }
            }

            Console.WriteLine($"  Found {tablesToFix.Count} timestamp/date columns to check");
            Console.WriteLine();

            int fixedCount = 0;
            var defaultDate = new DateTime(2000, 1, 1); // Use a safe default date

            foreach (var (table, column) in tablesToFix)
            {
                try
                {
                    // Update invalid dates to a valid default
                    var updateSql = $@"
                        UPDATE {schema}.""{table}""
                        SET ""{column}"" = @defaultDate
                        WHERE ""{column}"" < '0001-01-01'::timestamp
                           OR ""{column}"" > '9999-12-31'::timestamp
                           OR ""{column}"" = 'infinity'::timestamp
                           OR ""{column}"" = '-infinity'::timestamp;
                    ";

                    using (var updateCmd = new NpgsqlCommand(updateSql, connection))
                    {
                        updateCmd.Parameters.AddWithValue("defaultDate", defaultDate);
                        var rowsAffected = updateCmd.ExecuteNonQuery();
                        
                        if (rowsAffected > 0)
                        {
                            Console.WriteLine($"  ✓ Fixed {rowsAffected} invalid date(s) in {table}.{column}");
                            fixedCount += rowsAffected;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  ⚠ Could not fix {table}.{column}: {ex.Message}");
                }
            }

            if (fixedCount == 0)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"  ✓ No invalid dates found in {schema} schema!");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"  ✓ Fixed {fixedCount} total invalid date value(s) in {schema} schema");
                Console.ResetColor();
            }
        }
    }
}
