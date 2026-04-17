using Npgsql;
using System;

namespace Ombi.DatabaseFix
{
    public static class FindBadDates
    {
        public static void FindProblematicDates()
        {
            Console.WriteLine("==============================================");
            Console.WriteLine("Finding Exact Problematic DateTime Values");
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

            Console.WriteLine("Checking movierequests table...\n");

            // Check each date column in movierequests
            CheckColumn(connection, "movierequests", "requesteddate");
            CheckColumn(connection, "movierequests", "releasedate");
            CheckColumn(connection, "movierequests", "digitalreleasedate");
            CheckColumn(connection, "movierequests", "markedasapproved");
            CheckColumn(connection, "movierequests", "markedasavailable");
            CheckColumn(connection, "movierequests", "markedasdenied");
            CheckColumn(connection, "movierequests", "markedasapproved4k");
            CheckColumn(connection, "movierequests", "requesteddate4k");

            Console.WriteLine("\n==============================================");
            Console.WriteLine("Diagnostic complete");
            Console.WriteLine("==============================================\n");
        }

        private static void CheckColumn(NpgsqlConnection connection, string table, string column)
        {
            try
            {
                // Check for dates outside .NET's range (year 1-9999)
                var sql = $@"
                    SELECT 
                        id,
                        ""{column}"",
                        EXTRACT(YEAR FROM ""{column}"") as year
                    FROM ombi.""{table}""
                    WHERE ""{column}"" IS NOT NULL
                      AND (
                        EXTRACT(YEAR FROM ""{column}"") < 1
                        OR EXTRACT(YEAR FROM ""{column}"") > 9999
                        OR ""{column}"" = 'infinity'::timestamp
                        OR ""{column}"" = '-infinity'::timestamp
                      )
                    LIMIT 5;
                ";

                using var cmd = new NpgsqlCommand(sql, connection);
                using var reader = cmd.ExecuteReader();

                bool foundAny = false;
                while (reader.Read())
                {
                    if (!foundAny)
                    {
                        Console.WriteLine($"  ⚠ Found problematic dates in {table}.{column}:");
                        foundAny = true;
                    }

                    var id = reader.GetInt32(0);
                    var dateValue = reader.IsDBNull(1) ? "NULL" : reader.GetValue(1).ToString();
                    var year = reader.IsDBNull(2) ? "NULL" : reader.GetValue(2).ToString();

                    Console.WriteLine($"    - ID {id}: Year {year}, Value: {dateValue}");
                }

                if (!foundAny)
                {
                    Console.WriteLine($"  ✓ No problematic dates in {table}.{column}");
                }
            }
            catch (Exception ex)
            {
                if (!ex.Message.Contains("does not exist"))
                {
                    Console.WriteLine($"  ⚠ Error checking {table}.{column}: {ex.Message}");
                }
            }
        }

        public static void FixProblematicDates()
        {
            Console.WriteLine("==============================================");
            Console.WriteLine("Fixing ALL Out-of-Range DateTime Values");
            Console.WriteLine("Using .NET's actual range: year 1-9999");
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

            // Fix all date columns in all tables
            var columns = new[]
            {
                ("movierequests", "requesteddate"),
                ("movierequests", "releasedate"),
                ("movierequests", "digitalreleasedate"),
                ("movierequests", "markedasapproved"),
                ("movierequests", "markedasavailable"),
                ("movierequests", "markedasdenied"),
                ("movierequests", "markedasapproved4k"),
                ("movierequests", "requesteddate4k"),
                ("tvrequests", "requesteddate"),
                ("tvrequests", "markedasapproved"),
                ("tvrequests", "markedasavailable"),
                ("tvrequests", "markedasdenied"),
                ("childrequests", "requesteddate"),
                ("childrequests", "markedasapproved"),
                ("childrequests", "markedasavailable"),
                ("childrequests", "markedasdenied"),
                ("episoderequests", "airdate"),
                ("audit", "datetime"),
                ("issues", "issuecreated"),
                ("issues", "resolveddate"),
                ("issuecomments", "date")
            };

            foreach (var (table, column) in columns)
            {
                try
                {
                    // Fix dates outside year 1-9999 (full .NET DateTime range)
                    var sql = $@"
                        UPDATE ombi.""{table}""
                        SET ""{column}"" = '2000-01-01 00:00:00'::timestamp
                        WHERE ""{column}"" IS NOT NULL
                          AND (
                            EXTRACT(YEAR FROM ""{column}"") < 1
                            OR EXTRACT(YEAR FROM ""{column}"") > 9999
                            OR ""{column}"" = 'infinity'::timestamp
                            OR ""{column}"" = '-infinity'::timestamp
                          );
                    ";

                    using var cmd = new NpgsqlCommand(sql, connection);
                    var rowsAffected = cmd.ExecuteNonQuery();

                    if (rowsAffected > 0)
                    {
                        Console.WriteLine($"  ✓ Fixed {rowsAffected} invalid dates in {table}.{column}");
                        totalFixed += rowsAffected;
                    }
                }
                catch (Exception ex)
                {
                    if (!ex.Message.Contains("does not exist"))
                    {
                        Console.WriteLine($"  ⚠ Error fixing {table}.{column}: {ex.Message}");
                    }
                }
            }

            Console.WriteLine("\n==============================================");
            if (totalFixed > 0)
            {
                Console.WriteLine($"✓ Fixed {totalFixed} total invalid dates!");
            }
            else
            {
                Console.WriteLine("✓ No invalid dates found");
            }
            Console.WriteLine("==============================================\n");

            Console.WriteLine("Next steps:");
            Console.WriteLine("  1. Restart Ombi (Shift+F5 then F5)");
            Console.WriteLine("  2. DateTime errors should be resolved");
        }
    }
}
