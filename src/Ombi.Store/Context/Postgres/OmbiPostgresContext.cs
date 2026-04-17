using Microsoft.EntityFrameworkCore;
using Npgsql;
using System;
using System.Linq;

namespace Ombi.Store.Context.Postgres
{
    public sealed class OmbiPostgresContext : OmbiContext
    {
        private static bool _created;

        public OmbiPostgresContext(DbContextOptions<OmbiPostgresContext> options) : base(options)
        {
            if (_created) return;
            _created = true;

            try
            {
                // Check if database already has tables (migrated from MySQL)
                var canConnect = Database.CanConnect();
                if (!canConnect)
                {
                    Console.WriteLine("Cannot connect to Ombi database");
                    return;
                }

                // Try to get pending migrations
                var pendingMigrations = Database.GetPendingMigrations().ToList();

                if (pendingMigrations.Any())
                {
                    Console.WriteLine($"Applying {pendingMigrations.Count} pending migration(s) to Ombi database...");
                    Database.Migrate();
                    Console.WriteLine("Ombi database migrations completed.");
                }
                else
                {
                    Console.WriteLine("Ombi database is up to date.");
                }
            }
            catch (PostgresException ex) when (ex.SqlState == "42P07" || ex.SqlState == "42P04")
            {
                // 42P07: relation already exists
                // 42P04: database already exists
                Console.WriteLine($"Ombi database already configured (migrated from MySQL): {ex.MessageText}");
            }
            catch (Exception ex)
            {
                // Log but continue - database might already be set up from MySQL migration
                Console.WriteLine($"Ombi database note: {ex.Message}");
            }
        }

        public override void Dispose()
        {
            base.Dispose();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Configure PostgreSQL to use the 'ombi' schema where migrated data resides
            modelBuilder.HasDefaultSchema("ombi");

            base.OnModelCreating(modelBuilder);
        }
    }
}