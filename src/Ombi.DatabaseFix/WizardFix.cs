using System;
using System.Linq;
using System.IO;
using System.Text.Json;
using Npgsql;

namespace Ombi.DatabaseFix
{
    /// <summary>
    /// Marks the Ombi wizard as completed in the database
    /// Run this as: dotnet run --project Ombi.DatabaseFix -- skip-wizard
    /// </summary>
    class WizardFix
    {
        public static void SkipWizard()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("==============================================");
            Console.WriteLine("Ombi Wizard Skip Tool");
            Console.WriteLine("==============================================");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine("This will mark the wizard as completed in the database.");
            Console.WriteLine("Your data and admin users will NOT be affected.");
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

                // Get settings database connection string
                var connStr = doc.RootElement.GetProperty("SettingsDatabase").GetProperty("ConnectionString").GetString();
                
                Console.WriteLine("Connecting to settings database...");
                
                using var connection = new NpgsqlConnection(connStr);
                connection.Open();

                // Check all possible schema locations
                var schemas = new[] { "ombi", "public" };
                bool wizardSet = false;

                foreach (var schema in schemas)
                {
                    try
                    {
                        // Check if GlobalSettings table exists in this schema (case-insensitive)
                        var checkTableSql = $@"
                            SELECT table_name
                            FROM information_schema.tables 
                            WHERE table_schema = '{schema}' 
                            AND LOWER(table_name) = 'globalsettings';
                        ";

                        string? actualTableName = null;
                        using (var checkCmd = new NpgsqlCommand(checkTableSql, connection))
                        {
                            var result = checkCmd.ExecuteScalar();
                            if (result == null)
                            {
                                Console.WriteLine($"  GlobalSettings table not found in {schema} schema, trying next...");
                                continue;
                            }
                            actualTableName = result.ToString();
                        }

                        Console.WriteLine($"  Found {actualTableName} table in {schema} schema");

                        // Get actual column names (case-sensitive check)
                        var getColumnsSql = $@"
                            SELECT column_name
                            FROM information_schema.columns
                            WHERE table_schema = '{schema}'
                            AND table_name = '{actualTableName}'
                            ORDER BY column_name;
                        ";

                        string? settingsNameColumn = null;
                        string? contentColumn = null;

                        using (var columnsCmd = new NpgsqlCommand(getColumnsSql, connection))
                        using (var reader = columnsCmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var colName = reader.GetString(0);
                                if (colName.ToLower() == "settingsname")
                                    settingsNameColumn = colName;
                                else if (colName.ToLower() == "content")
                                    contentColumn = colName;
                            }
                        }

                        if (settingsNameColumn == null || contentColumn == null)
                        {
                            Console.WriteLine($"  Required columns not found in {schema}.{actualTableName}");
                            continue;
                        }

                        Console.WriteLine($"  Using columns: {settingsNameColumn}, {contentColumn}");

                        // Check if OmbiSettings exists
                        var checkSettingsSql = $@"
                            SELECT COUNT(*) 
                            FROM {schema}.""{actualTableName}""
                            WHERE ""{settingsNameColumn}"" = 'OmbiSettings';
                        ";

                        using (var checkSettingsCmd = new NpgsqlCommand(checkSettingsSql, connection))
                        {
                            var count = Convert.ToInt32(checkSettingsCmd.ExecuteScalar());
                            if (count == 0)
                            {
                                Console.WriteLine($"  OmbiSettings record not found in {schema}.{actualTableName}");
                                Console.WriteLine($"  Creating OmbiSettings record with Wizard=true...");

                                // Create the OmbiSettings record
                                var insertSql = $@"
                                    INSERT INTO {schema}.""{actualTableName}"" (""{settingsNameColumn}"", ""{contentColumn}"")
                                    VALUES ('OmbiSettings', '{{""Wizard"": true}}'::jsonb);
                                ";

                                using (var insertCmd = new NpgsqlCommand(insertSql, connection))
                                {
                                    insertCmd.ExecuteNonQuery();
                                    Console.ForegroundColor = ConsoleColor.Green;
                                    Console.WriteLine($"  ✓ Created OmbiSettings with Wizard=true!");
                                    Console.ResetColor();
                                    wizardSet = true;
                                    break;
                                }
                            }
                        }

                        // Update the wizard flag
                        var updateSql = $@"
                            UPDATE {schema}.""{actualTableName}"" 
                            SET ""{contentColumn}"" = jsonb_set(""{contentColumn}""::jsonb, '{{Wizard}}', 'true'::jsonb)
                            WHERE ""{settingsNameColumn}"" = 'OmbiSettings';
                        ";

                        using (var updateCmd = new NpgsqlCommand(updateSql, connection))
                        {
                            var rowsAffected = updateCmd.ExecuteNonQuery();

                            if (rowsAffected > 0)
                            {
                                Console.ForegroundColor = ConsoleColor.Green;
                                Console.WriteLine($"  ✓ Wizard flag set to true in {schema}.{actualTableName}!");
                                Console.ResetColor();
                                wizardSet = true;
                                break;
                            }
                            else
                            {
                                Console.WriteLine($"  Could not update OmbiSettings in {schema}.{actualTableName}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  Error with {schema} schema: {ex.Message}");
                    }
                }

                if (!wizardSet)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine();
                    Console.WriteLine("Could not find or update OmbiSettings.");
                    Console.WriteLine("The settings may not exist yet - you may need to complete the wizard once.");
                    Console.ResetColor();
                }
                else
                {
                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("==============================================");
                    Console.WriteLine("✓ Wizard marked as completed!");
                    Console.WriteLine("==============================================");
                    Console.ResetColor();
                    Console.WriteLine();
                    Console.WriteLine("Next steps:");
                    Console.WriteLine("  1. Restart Ombi (F5 in Visual Studio)");
                    Console.WriteLine("  2. You should now see the login page instead of wizard");
                    Console.WriteLine("  3. Login with your existing admin credentials");
                }
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
    }
}
