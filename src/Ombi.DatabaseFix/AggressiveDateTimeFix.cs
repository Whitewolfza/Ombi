using Npgsql;
using System;

namespace Ombi.DatabaseFix
{
    public static class AggressiveDateTimeFix
    {
        public static void FixAllDates()
        {
            Console.WriteLine("==============================================");
            Console.WriteLine("Aggressive DateTime Fix - Direct SQL Approach");
            Console.WriteLine("==============================================\n");

            var config = Program.ReadDatabaseConfig();
            if (config == null)
            {
                Console.WriteLine("Failed to read database configuration");
                return;
            }

            // Build the connection string from the config parts
            var connString = $"Host={config.Host};Port={config.Port};Database={config.OmbiDatabase};Username={config.Username};Password={config.Password}";

            Console.WriteLine("Connecting to Ombi database...\n");

            using var connection = new NpgsqlConnection(connString);
            connection.Open();

            // List of tables and their date columns that commonly have issues
            var tablesToFix = new[]
            {
                new { Table = "movierequests", Columns = new[] { "requestedate", "releasedate", "digitialreleasedate", "markedashostadded", "markedashavailable" } },
                new { Table = "tvrequests", Columns = new[] { "requestedate", "markedashostadded", "markedashavailable" } },
                new { Table = "childrequests", Columns = new[] { "requestedate", "markedashostadded", "markedashavailable" } },
                new { Table = "episoderequests", Columns = new[] { "airdate" } },
                new { Table = "audit", Columns = new[] { "datetime" } },
                new { Table = "issues", Columns = new[] { "issuecreateed", "resolveeddate" } },
                new { Table = "issuecomments", Columns = new[] { "date" } }
            };

            int totalFixed = 0;

            foreach (var tableInfo in tablesToFix)
            {
                Console.WriteLine($"Fixing table: ombi.{tableInfo.Table}");

                foreach (var column in tableInfo.Columns)
                {
                    try
                    {
                        // Use a safe date range: '1900-01-01' to '2100-12-31'
                        // This is much safer than the full .NET range
                        var sql = $@"
                            UPDATE ombi.""{tableInfo.Table}""
                            SET ""{column}"" = '2000-01-01 00:00:00'::timestamp
                            WHERE ""{column}"" IS NOT NULL
                              AND (
                                ""{column}"" < '1900-01-01'::timestamp
                                OR ""{column}"" > '2100-12-31'::timestamp
                                OR ""{column}"" = 'infinity'::timestamp
                                OR ""{column}"" = '-infinity'::timestamp
                              );
                        ";

                        using var cmd = new NpgsqlCommand(sql, connection);
                        var rowsAffected = cmd.ExecuteNonQuery();

                        if (rowsAffected > 0)
                        {
                            Console.WriteLine($"  ✓ Fixed {rowsAffected} invalid dates in column '{column}'");
                            totalFixed += rowsAffected;
                        }
                    }
                    catch (Exception ex)
                    {
                        // Column might not exist in this table, just skip
                        if (!ex.Message.Contains("does not exist"))
                        {
                            Console.WriteLine($"  ⚠ Error fixing '{column}': {ex.Message}");
                        }
                    }
                }

                Console.WriteLine();
            }

            Console.WriteLine("==============================================");
            if (totalFixed > 0)
            {
                Console.WriteLine($"✓ Fixed {totalFixed} total invalid dates!");
            }
            else
            {
                Console.WriteLine("✓ No invalid dates found (or already fixed)");
            }
            Console.WriteLine("==============================================\n");

            Console.WriteLine("Next steps:");
            Console.WriteLine("  1. Restart Ombi (Shift+F5 then F5)");
            Console.WriteLine("  2. DateTime errors should be resolved");
            Console.WriteLine("  3. Historical requests should now be visible");
        }

        public static void DiagnoseDates()
        {
            Console.WriteLine("==============================================");
            Console.WriteLine("DateTime Diagnostic - Finding Problematic Dates");
            Console.WriteLine("==============================================\n");

            var config = Program.ReadDatabaseConfig();
            if (config == null)
            {
                Console.WriteLine("Failed to read database configuration");
                return;
            }

            // Build the connection string from the config parts
            var connString = $"Host={config.Host};Port={config.Port};Database={config.OmbiDatabase};Username={config.Username};Password={config.Password}";

            using var connection = new NpgsqlConnection(connString);
            connection.Open();

            // Check movierequests table specifically (where the error is happening)
            var sql = @"
                SELECT 
                    'movierequests' as table_name,
                    'requestedate' as column_name,
                    COUNT(*) as count
                FROM ombi.movierequests
                WHERE requestedate < '1900-01-01'::timestamp OR requestedate > '2100-12-31'::timestamp
                
                UNION ALL
                
                SELECT 
                    'movierequests',
                    'releasedate',
                    COUNT(*)
                FROM ombi.movierequests
                WHERE releasedate < '1900-01-01'::timestamp OR releasedate > '2100-12-31'::timestamp
                
                UNION ALL
                
                SELECT 
                    'movierequests',
                    'digitialreleasedate',
                    COUNT(*)
                FROM ombi.movierequests
                WHERE digitialreleasedate < '1900-01-01'::timestamp OR digitialreleasedate > '2100-12-31'::timestamp;
            ";

            using var cmd = new NpgsqlCommand(sql, connection);
            using var reader = cmd.ExecuteReader();

            bool foundIssues = false;
            while (reader.Read())
            {
                var table = reader.GetString(0);
                var column = reader.GetString(1);
                var count = reader.GetInt64(2);

                if (count > 0)
                {
                    Console.WriteLine($"⚠ Found {count} problematic dates in {table}.{column}");
                    foundIssues = true;
                }
            }

            if (!foundIssues)
            {
                Console.WriteLine("✓ No problematic dates found in movierequests table");
            }

            Console.WriteLine();
        }
    }
}
