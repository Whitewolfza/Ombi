using Npgsql;
using System;

namespace Ombi.DatabaseFix
{
    public static class ShowActualValues
    {
        public static void ShowProblematicColumnValues()
        {
            Console.WriteLine("==============================================");
            Console.WriteLine("Showing Actual Values in Problematic Columns");
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

            // Query the first 10 records and show raw values
            var sql = @"
                SELECT 
                    id,
                    title,
                    markedasdenied,
                    markedasapproved4k,
                    requesteddate4k,
                    EXTRACT(YEAR FROM markedasdenied) as denied_year,
                    EXTRACT(YEAR FROM markedasapproved4k) as approved4k_year,
                    EXTRACT(YEAR FROM requesteddate4k) as requested4k_year,
                    pg_typeof(markedasdenied) as denied_type,
                    pg_typeof(markedasapproved4k) as approved4k_type,
                    pg_typeof(requesteddate4k) as requested4k_type
                FROM ombi.movierequests
                LIMIT 10;
            ";

            using var cmd = new NpgsqlCommand(sql, connection);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                var id = reader.GetInt32(0);
                var title = reader.IsDBNull(1) ? "NULL" : reader.GetString(1);
                
                Console.WriteLine($"\n--- Record ID {id}: {title} ---");
                
                // Show raw values for each problematic column
                for (int i = 2; i <= 4; i++)
                {
                    string colName = i == 2 ? "markedasdenied" : i == 3 ? "markedasapproved4k" : "requesteddate4k";
                    
                    if (!reader.IsDBNull(i))
                    {
                        try
                        {
                            // Try to get as object first
                            var rawValue = reader.GetValue(i);
                            var year = reader.GetValue(i + 3);
                            var type = reader.GetString(i + 6);
                            
                            Console.WriteLine($"  {colName}:");
                            Console.WriteLine($"    Raw value: {rawValue}");
                            Console.WriteLine($"    Type: {rawValue.GetType().Name}");
                            Console.WriteLine($"    PG Type: {type}");
                            Console.WriteLine($"    Year: {year}");
                            
                            // Try to read as DateTime
                            try
                            {
                                var dt = reader.GetDateTime(i);
                                Console.WriteLine($"    GetDateTime() SUCCESS: {dt}");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"    GetDateTime() FAILED: {ex.Message}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"  {colName}: ERROR - {ex.Message}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"  {colName}: NULL");
                    }
                }
            }

            Console.WriteLine("\n==============================================");
            Console.WriteLine("Diagnostic complete");
            Console.WriteLine("==============================================\n");
        }
    }
}
