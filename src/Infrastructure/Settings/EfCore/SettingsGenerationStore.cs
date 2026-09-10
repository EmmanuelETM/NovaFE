using NovaFE.Application.Settings.Interfaces;
using NovaFE.Infrastructure.Persistence.EfCore;
using Microsoft.EntityFrameworkCore;

namespace NovaFE.Infrastructure.Settings.EfCore;

/// <summary>
/// El contador de generación (<c>settings_generation</c>, fila única <c>id = 1</c>).
/// Sobre <c>AppDbContext</c> para que el bump caiga en la misma transacción que la
/// escritura del setting.
/// </summary>
internal sealed class SettingsGenerationStore(AppDbContext context) : ISettingsGenerationStore
{
    public async Task<long> CurrentAsync(CancellationToken ct = default)
    {
        var rows = await context.Database
            .SqlQueryRaw<long>("SELECT value AS \"Value\" FROM settings_generation WHERE id = 1")
            .ToListAsync(ct);

        return rows.Count > 0 ? rows[0] : 0;
    }

    public Task BumpAsync(CancellationToken ct = default) =>
        context.Database.ExecuteSqlRawAsync(
            "UPDATE settings_generation SET value = value + 1 WHERE id = 1", ct);
}
