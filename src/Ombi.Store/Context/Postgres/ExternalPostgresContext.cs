using Microsoft.EntityFrameworkCore;
using Npgsql;
using System;
using System.Linq;

namespace Ombi.Store.Context.Postgres
{
    public sealed class ExternalPostgresContext : ExternalContext
    {
        private static bool _created;
        public ExternalPostgresContext(DbContextOptions<ExternalPostgresContext> options) : base(options)
        {
            if (_created) return;

            _created = true;
            try
            {
                // Check if database already has tables (migrated from MySQL)
                var canConnect = Database.CanConnect();
                if (!canConnect)
                {
                    Console.WriteLine("Cannot connect to External database");
                    return;
                }

                // Try to get pending migrations
                var pendingMigrations = Database.GetPendingMigrations().ToList();

                if (pendingMigrations.Any())
                {
                    Console.WriteLine($"Applying {pendingMigrations.Count} pending migration(s) to External database...");
                    Database.Migrate();
                    Console.WriteLine("External database migrations completed.");
                }
                else
                {
                    Console.WriteLine("External database is up to date.");
                }
            }
            catch (PostgresException ex) when (ex.SqlState == "42P07" || ex.SqlState == "42P04")
            {
                // 42P07: relation already exists
                // 42P04: database already exists
                Console.WriteLine($"External database already configured (migrated from MySQL): {ex.MessageText}");
            }
            catch (Exception ex)
            {
                // Log but continue - database might already be set up from MySQL migration
                Console.WriteLine($"External database note: {ex.Message}");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Configure PostgreSQL to use the 'ombi' schema where migrated data resides
            modelBuilder.HasDefaultSchema("ombi");

            base.OnModelCreating(modelBuilder);
        }
    }
}