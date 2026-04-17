using Npgsql;
using System;

namespace Ombi.DatabaseFix
{
    public static class ReadDateTest
    {
        public static void TestReadDates()
        {
            Console.WriteLine("==============================================");
            Console.WriteLine("Testing Actual DateTime Reading (Like EF Core)");
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

            Console.WriteLine("Attempting to read ALL movierequests records...\n");

            var sql = @"
                SELECT 
                    id,
                    title,
                    requesteddate,
                    releasedate,
                    digitalreleasedate,
                    markedasapproved,
                    markedasavailable,
                    markedasdenied,
                    markedasapproved4k,
                    requesteddate4k
                FROM ombi.movierequests
                LIMIT 10;
            ";

            using var cmd = new NpgsqlCommand(sql, connection);
            using var reader = cmd.ExecuteReader();

            int successCount = 0;
            int errorCount = 0;

            while (reader.Read())
            {
                try
                {
                    var id = reader.GetInt32(0);
                    var title = reader.IsDBNull(1) ? "NULL" : reader.GetString(1);

                    // Try to read each DateTime column like EF Core would
                    DateTime requestedDate = reader.GetDateTime(2);
                    DateTime releaseDate = reader.GetDateTime(3);
                    DateTime? digitalReleaseDate = reader.IsDBNull(4) ? null : reader.GetDateTime(4);
                    DateTime markedAsApproved = reader.GetDateTime(5);
                    DateTime? markedAsAvailable = reader.IsDBNull(6) ? null : reader.GetDateTime(6);
                    DateTime markedAsDenied = reader.GetDateTime(7);
                    DateTime markedAsApproved4k = reader.GetDateTime(8);
                    DateTime requestedDate4k = reader.GetDateTime(9);

                    successCount++;
                }
                catch (InvalidCastException ex)
                {
                    errorCount++;
                    Console.WriteLine($"  ⚠ ERROR reading ID {reader.GetInt32(0)}: {ex.Message}");
                    
                    // Show which column caused the error
                    for (int i = 2; i <= 9; i++)
                    {
                        if (!reader.IsDBNull(i))
                        {
                            try
                            {
                                var val = reader.GetValue(i);
                                Console.WriteLine($"     Column {i}: {val} (Type: {val.GetType().Name})");
                            }
                            catch (Exception colEx)
                            {
                                Console.WriteLine($"     Column {i}: ERROR - {colEx.Message}");
                            }
                        }
                    }
                    
                    if (errorCount >= 5)
                    {
                        Console.WriteLine("\n  Stopping after 5 errors...\n");
                        break;
                    }
                }
                catch (Exception ex)
                {
                    errorCount++;
                    Console.WriteLine($"  ⚠ Unexpected error reading ID {reader.GetInt32(0)}: {ex.GetType().Name} - {ex.Message}");
                    
                    if (errorCount >= 5)
                    {
                        Console.WriteLine("\n  Stopping after 5 errors...\n");
                        break;
                    }
                }
            }

            Console.WriteLine($"\n==============================================");
            Console.WriteLine($"Results:");
            Console.WriteLine($"  ✓ Successfully read: {successCount} records");
            Console.WriteLine($"  ⚠ Errors encountered: {errorCount} records");
            Console.WriteLine($"==============================================\n");
        }
    }
}
