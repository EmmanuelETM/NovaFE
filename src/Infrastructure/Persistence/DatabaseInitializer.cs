using NovaFE.Application.Common.Interfaces;
using NovaFE.Infrastructure.Persistence.EfCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NovaFE.Infrastructure.Persistence;

/// <summary>
/// Aplica las migraciones pendientes y corre los <see cref="IDataSeeder"/> al
/// arrancar, si <c>Database:MigrateOnStartup</c> está activo. Se llama desde
/// <c>Program.cs</c> antes de <c>app.Run()</c>.
/// </summary>
public static class DatabaseInitializer
{
    // Clave arbitraria y fija para el lock consultivo de PostgreSQL: si arrancan
    // varias instancias a la vez, solo una migra; las demás esperan y encuentran
    // la base ya al día.
    private const long StartupLockKey = 5_713_190_012L;

    public static async Task MigrateAndSeedDatabaseAsync(this IHost host, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(host);

        await using var scope = host.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;

        var options = services.GetRequiredService<IOptions<DatabaseOptions>>().Value;
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Database.Initializer");

        if (!options.MigrateOnStartup)
        {
            logger.LogDebug("Database:MigrateOnStartup está desactivado; no se aplican migraciones ni seeds.");
            return;
        }

        var context = services.GetRequiredService<AppDbContext>();
        var connection = context.Database.GetDbConnection();

        await connection.OpenAsync(ct);

        await using (var acquire = connection.CreateCommand())
        {
            acquire.CommandText = $"SELECT pg_advisory_lock({StartupLockKey})";
            await acquire.ExecuteNonQueryAsync(ct);
        }

        try
        {
            var pending = (await context.Database.GetPendingMigrationsAsync(ct)).ToList();
            if (pending.Count > 0)
            {
                if (logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("Aplicando {Count} migración(es) pendiente(s): {Migrations}",
                        pending.Count, string.Join(", ", pending));
                }

                await context.Database.MigrateAsync(ct);
            }
            else
            {
                logger.LogInformation("La base de datos ya está al día; no hay migraciones pendientes.");
            }

            var seeders = services.GetServices<IDataSeeder>().OrderBy(s => s.Order).ToList();
            foreach (var seeder in seeders)
            {
                if (logger.IsEnabled(LogLevel.Information))
                    logger.LogInformation("Ejecutando seeder {Seeder} (orden {Order})",
                        seeder.GetType().Name, seeder.Order);

                await seeder.SeedAsync(ct);
            }
        }
        finally
        {
            await using var release = connection.CreateCommand();
            release.CommandText = $"SELECT pg_advisory_unlock({StartupLockKey})";
            await release.ExecuteNonQueryAsync(ct);
        }
    }
}
