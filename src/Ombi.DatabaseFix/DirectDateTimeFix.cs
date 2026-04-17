using Npgsql;
using System;

namespace Ombi.DatabaseFix
{
    public static class DirectDateTimeFix
    {
        public static void FixAllDateTimesNow()
        {
            Console.WriteLine("==============================================");
            Console.WriteLine("Direct DateTime Fix - No Diagnostics");
            Console.WriteLine("Fixing ALL timestamp columns with invalid timezones");
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

            Console.WriteLine("Fixing movierequests table...");
            var movieSql = @"
                UPDATE ombi.movierequests
                SET 
                    requesteddate = CASE WHEN requesteddate::text LIKE '%+01:52' THEN '2020-01-01 00:00:00'::timestamp ELSE requesteddate END,
                    releasedate = CASE WHEN releasedate::text LIKE '%+01:52' THEN '2020-01-01 00:00:00'::timestamp ELSE releasedate END,
                    digitalreleasedate = CASE WHEN digitalreleasedate::text LIKE '%+01:52' THEN '2020-01-01 00:00:00'::timestamp ELSE digitalreleasedate END,
                    markedasapproved = CASE WHEN markedasapproved::text LIKE '%+01:52' THEN '0001-01-01 00:00:00'::timestamp ELSE markedasapproved END,
                    markedasavailable = CASE WHEN markedasavailable::text LIKE '%+01:52' THEN NULL ELSE markedasavailable END,
                    markedasdenied = CASE WHEN markedasdenied::text LIKE '%+01:52' THEN '0001-01-01 00:00:00'::timestamp ELSE markedasdenied END,
                    markedasapproved4k = CASE WHEN markedasapproved4k::text LIKE '%+01:52' THEN '0001-01-01 00:00:00'::timestamp ELSE markedasapproved4k END,
                    requesteddate4k = CASE WHEN requesteddate4k::text LIKE '%+01:52' THEN '0001-01-01 00:00:00'::timestamp ELSE requesteddate4k END;
            ";

            using (var cmd = new NpgsqlCommand(movieSql, connection))
            {
                var rows = cmd.ExecuteNonQuery();
                totalFixed += rows;
                Console.WriteLine($"  ✓ Updated movierequests ({rows} rows affected)");
            }

            Console.WriteLine("Fixing tvrequests table...");
            var tvSql = @"
                UPDATE ombi.tvrequests
                SET 
                    requesteddate = CASE WHEN requesteddate::text LIKE '%+01:52' THEN '2020-01-01 00:00:00'::timestamp ELSE requesteddate END,
                    markedasapproved = CASE WHEN markedasapproved::text LIKE '%+01:52' THEN '0001-01-01 00:00:00'::timestamp ELSE markedasapproved END,
                    markedasavailable = CASE WHEN markedasavailable::text LIKE '%+01:52' THEN NULL ELSE markedasavailable END;
            ";

            try
            {
                using (var cmd = new NpgsqlCommand(tvSql, connection))
                {
                    var rows = cmd.ExecuteNonQuery();
                    totalFixed += rows;
                    Console.WriteLine($"  ✓ Updated tvrequests ({rows} rows affected)");
                }
            }
            catch (Exception ex)
            {
                if (!ex.Message.Contains("does not exist"))
                {
                    Console.WriteLine($"  ⚠ Error: {ex.Message}");
                }
            }

            Console.WriteLine("Fixing childrequests table...");
            var childSql = @"
                UPDATE ombi.childrequests
                SET 
                    requesteddate = CASE WHEN requesteddate::text LIKE '%+01:52' THEN '2020-01-01 00:00:00'::timestamp ELSE requesteddate END,
                    markedasapproved = CASE WHEN markedasapproved::text LIKE '%+01:52' THEN '0001-01-01 00:00:00'::timestamp ELSE markedasapproved END,
                    markedasavailable = CASE WHEN markedasavailable::text LIKE '%+01:52' THEN NULL ELSE markedasavailable END;
            ";

            try
            {
                using (var cmd = new NpgsqlCommand(childSql, connection))
                {
                    var rows = cmd.ExecuteNonQuery();
                    totalFixed += rows;
                    Console.WriteLine($"  ✓ Updated childrequests ({rows} rows affected)");
                }
            }
            catch (Exception ex)
            {
                if (!ex.Message.Contains("does not exist"))
                {
                    Console.WriteLine($"  ⚠ Error: {ex.Message}");
                }
            }

            Console.WriteLine($"\n==============================================");
            Console.WriteLine($"✓ Fix complete! Total rows updated: {totalFixed}");
            Console.WriteLine($"==============================================\n");
            Console.WriteLine("Next steps:");
            Console.WriteLine("  1. Start Ombi (F5 in Visual Studio)");
            Console.WriteLine("  2. DateTime errors should be GONE");
            Console.WriteLine("  3. Historical requests should now be visible\n");
        }
    }
}
