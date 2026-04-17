using Npgsql;
using System;

namespace Ombi.DatabaseFix
{
    public static class TargetedDateTimeFix
    {
        public static void FixNonNullableDateColumns()
        {
            Console.WriteLine("==============================================");
            Console.WriteLine("Fixing Non-Nullable DateTime Columns");
            Console.WriteLine("Targeting: markedasdenied, markedasapproved4k, requesteddate4k");
            Console.WriteLine("==============================================\n");

            var config = Program.ReadDatabaseConfig();
            if (config == null)
            {
                Console.WriteLine("Failed to read database configuration");
                return;
            }

            var connString = $"Host={config.Host};Port={config.Port};Database={config.OmbiDatabase};Username={config.Username};Password={config.Password}";

            using var connection = new NpgsqlConnection(connString);
            connection.Open();

            int totalFixed = 0;

            // These are non-nullable DateTime columns that MySQL defaulted to 0000-00-00
            // We'll set them to a safe default: 0001-01-01 (DateTime.MinValue equivalent)
            var fixOperations = new[]
            {
                ("movierequests", "markedasdenied"),
                ("movierequests", "markedasapproved4k"),
                ("movierequests", "requesteddate4k"),
                ("tvrequests", "markedasdenied"),
                ("childrequests", "markedasdenied")
            };

            foreach (var (table, column) in fixOperations)
            {
                try
                {
                    Console.WriteLine($"Fixing {table}.{column}...");

                    // First, let's see what values are causing problems
                    var checkSql = $@"
                        SELECT COUNT(*) 
                        FROM ombi.""{table}""
                        WHERE ""{column}"" IS NOT NULL
                          AND (
                            EXTRACT(YEAR FROM ""{column}"") < 1
                            OR EXTRACT(YEAR FROM ""{column}"") > 9999
                            OR ""{column}"" = 'infinity'::timestamp
                            OR ""{column}"" = '-infinity'::timestamp
                          );
                    ";

                    using (var checkCmd = new NpgsqlCommand(checkSql, connection))
                    {
                        var count = (long)checkCmd.ExecuteScalar();
                        Console.WriteLine($"  Found {count} problematic dates");

                        if (count == 0)
                        {
                            Console.WriteLine($"  ✓ No problematic dates in {table}.{column}");
                            continue;
                        }
                    }

                    // Fix: Set to DateTime.MinValue (0001-01-01)
                    var fixSql = $@"
                        UPDATE ombi.""{table}""
                        SET ""{column}"" = '0001-01-01 00:00:00'::timestamp
                        WHERE ""{column}"" IS NOT NULL
                          AND (
                            EXTRACT(YEAR FROM ""{column}"") < 1
                            OR EXTRACT(YEAR FROM ""{column}"") > 9999
                            OR ""{column}"" = 'infinity'::timestamp
                            OR ""{column}"" = '-infinity'::timestamp
                          );
                    ";

                    using (var fixCmd = new NpgsqlCommand(fixSql, connection))
                    {
                        var rowsAffected = fixCmd.ExecuteNonQuery();
                        totalFixed += rowsAffected;
                        Console.WriteLine($"  ✓ Fixed {rowsAffected} rows in {table}.{column}");
                    }
                }
                catch (Exception ex)
                {
                    if (ex.Message.Contains("does not exist"))
                    {
                        Console.WriteLine($"  ⓘ Table/column {table}.{column} does not exist (OK)");
                    }
                    else
                    {
                        Console.WriteLine($"  ⚠ Error fixing {table}.{column}: {ex.Message}");
                    }
                }
            }

            Console.WriteLine($"\n==============================================");
            Console.WriteLine($"Total rows fixed: {totalFixed}");
            Console.WriteLine($"==============================================\n");
            Console.WriteLine("Next steps:");
            Console.WriteLine("  1. Restart Ombi (Shift+F5 then F5)");
            Console.WriteLine("  2. DateTime errors should be resolved");
            Console.WriteLine("  3. Historical requests should now be visible\n");
        }
    }
}
