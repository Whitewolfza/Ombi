using Npgsql;
using System;

namespace Ombi.DatabaseFix
{
    public static class ShowRawText
    {
        public static void ShowAsText()
        {
            Console.WriteLine("==============================================");
            Console.WriteLine("Showing Raw Text Values (Bypass Type Conversion)");
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

            // Query values as TEXT to bypass Npgsql's DateTime conversion
            var sql = @"
                SELECT 
                    id,
                    title,
                    markedasdenied::text as denied_text,
                    markedasapproved4k::text as approved4k_text,
                    requesteddate4k::text as requested4k_text,
                    EXTRACT(YEAR FROM markedasdenied)::text as denied_year,
                    EXTRACT(YEAR FROM markedasapproved4k)::text as approved4k_year,
                    EXTRACT(YEAR FROM requesteddate4k)::text as requested4k_year
                FROM ombi.movierequests
                WHERE id IN (33, 39, 44, 45, 46)
                LIMIT 10;
            ";

            using var cmd = new NpgsqlCommand(sql, connection);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                var id = reader.GetInt32(0);
                var title = reader.IsDBNull(1) ? "NULL" : reader.GetString(1);
                
                Console.WriteLine($"\n--- Record ID {id}: {title} ---");
                
                var deniedText = reader.IsDBNull(2) ? "NULL" : reader.GetString(2);
                var approved4kText = reader.IsDBNull(3) ? "NULL" : reader.GetString(3);
                var requested4kText = reader.IsDBNull(4) ? "NULL" : reader.GetString(4);
                
                var deniedYear = reader.IsDBNull(5) ? "NULL" : reader.GetString(5);
                var approved4kYear = reader.IsDBNull(6) ? "NULL" : reader.GetString(6);
                var requested4kYear = reader.IsDBNull(7) ? "NULL" : reader.GetString(7);
                
                Console.WriteLine($"  markedasdenied:");
                Console.WriteLine($"    Raw Text: {deniedText}");
                Console.WriteLine($"    Year: {deniedYear}");
                
                Console.WriteLine($"  markedasapproved4k:");
                Console.WriteLine($"    Raw Text: {approved4kText}");
                Console.WriteLine($"    Year: {approved4kYear}");
                
                Console.WriteLine($"  requesteddate4k:");
                Console.WriteLine($"    Raw Text: {requested4kText}");
                Console.WriteLine($"    Year: {requested4kYear}");
            }

            Console.WriteLine("\n==============================================");
            Console.WriteLine("Creating fix based on actual values...");
            Console.WriteLine("==============================================\n");

            // Close the reader before executing updates
            reader.Close();

            // Now fix by updating all these columns to a safe default without timezone
            Console.WriteLine("Executing fix...\n");

            var fixSql = @"
                UPDATE ombi.movierequests
                SET 
                    markedasdenied = '0001-01-01 00:00:00'::timestamp,
                    markedasapproved4k = '0001-01-01 00:00:00'::timestamp,
                    requesteddate4k = '0001-01-01 00:00:00'::timestamp
                WHERE markedasdenied::text LIKE '%+01:52'
                   OR markedasapproved4k::text LIKE '%+01:52'
                   OR requesteddate4k::text LIKE '%+01:52';
            ";

            using (var fixCmd = new NpgsqlCommand(fixSql, connection))
            {
                var rowsAffected = fixCmd.ExecuteNonQuery();
                Console.WriteLine($"✓ Fixed {rowsAffected} rows in movierequests");
            }
            
            // Also fix tvrequests and childrequests if they have the same issue
            var tables = new[] { "tvrequests", "childrequests" };
            foreach (var table in tables)
            {
                try
                {
                    var tableFixSql = $@"
                        UPDATE ombi.""{table}""
                        SET markedasdenied = '0001-01-01 00:00:00'::timestamp
                        WHERE markedasdenied::text LIKE '%+01:52';
                    ";

                    using (var tableFixCmd = new NpgsqlCommand(tableFixSql, connection))
                    {
                        var rowsAffected = tableFixCmd.ExecuteNonQuery();
                        if (rowsAffected > 0)
                        {
                            Console.WriteLine($"✓ Fixed {rowsAffected} rows in {table}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (!ex.Message.Contains("does not exist"))
                    {
                        Console.WriteLine($"⚠ Error fixing {table}: {ex.Message}");
                    }
                }
            }
            
            Console.WriteLine("\n==============================================");
            Console.WriteLine("Fix complete!");
            Console.WriteLine("==============================================\n");
            Console.WriteLine("Next steps:");
            Console.WriteLine("  1. Restart Ombi (Shift+F5 then F5)");
            Console.WriteLine("  2. DateTime errors should be resolved");
            Console.WriteLine("  3. Historical requests should now be visible\n");
        }
    }
}
