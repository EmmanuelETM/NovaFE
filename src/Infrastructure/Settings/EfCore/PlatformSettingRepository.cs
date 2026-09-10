using NovaFE.Application.Settings.Contracts;
using NovaFE.Application.Settings.Interfaces;
using NovaFE.Infrastructure.Persistence.EfCore;
using Microsoft.EntityFrameworkCore;

namespace NovaFE.Infrastructure.Settings.EfCore;

/// <summary>
/// Write side de los overrides de plataforma (ambiente agnóstico, <c>environment = ""</c>).
/// Los repos persisten de inmediato; el caso de uso los agrupa con la bitácora y
/// el bump de generación en una <c>IUnitOfWork.ExecuteInTransactionAsync</c>.
/// </summary>
internal sealed class PlatformSettingRepository(AppDbContext context) : IPlatformSettingRepository
{
    public async Task<PlatformSettingRecord?> GetAsync(string key, CancellationToken ct = default)
    {
        var row = await Query(key).FirstOrDefaultAsync(ct);
        return row is null ? null : ToRecord(row);
    }

    public async Task UpsertAsync(string key, string value, CancellationToken ct = default)
    {
        var row = await Query(key).FirstOrDefaultAsync(ct);

        if (row is null)
            await context.PlatformSettings.AddAsync(new PlatformSetting(key, value), ct);
        else
            row.SetValue(value);

        await context.SaveChangesAsync(ct);
    }

    public async Task<bool> RemoveAsync(string key, CancellationToken ct = default)
    {
        var row = await Query(key).FirstOrDefaultAsync(ct);
        if (row is null)
            return false;

        context.PlatformSettings.Remove(row);
        await context.SaveChangesAsync(ct);
        return true;
    }

    private IQueryable<PlatformSetting> Query(string key) =>
        context.PlatformSettings.Where(s => s.Key == key && s.Environment == "");

    private static PlatformSettingRecord ToRecord(PlatformSetting row) => new(
        row.Key,
        row.Environment,
        row.Value,
        row.UpdatedAt ?? row.CreatedAt,
        row.UpdatedBy ?? row.CreatedBy);
}
