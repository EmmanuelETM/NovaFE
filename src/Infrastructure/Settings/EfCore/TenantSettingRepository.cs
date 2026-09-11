using NovaFE.Application.Settings.Contracts;
using NovaFE.Application.Settings.Interfaces;
using NovaFE.Infrastructure.Persistence.EfCore;
using Microsoft.EntityFrameworkCore;

namespace NovaFE.Infrastructure.Settings.EfCore;

/// <summary>
/// Write side de los overrides de settings de tenant (ambiente agnóstico,
/// <c>environment = ""</c>). El filtro global de EF ya acota por <c>TenantId</c> y
/// el interceptor lo estampa al insertar; el caso de uso agrupa la escritura con
/// la bitácora en una <c>IUnitOfWork.ExecuteInTransactionAsync</c>.
/// </summary>
internal sealed class TenantSettingRepository(AppDbContext context) : ITenantSettingRepository
{
    public async Task<TenantSettingRecord?> GetAsync(string key, CancellationToken ct = default)
    {
        var row = await Query(key).FirstOrDefaultAsync(ct);
        return row is null ? null : ToRecord(row);
    }

    public async Task UpsertAsync(string key, string value, CancellationToken ct = default)
    {
        var row = await Query(key).FirstOrDefaultAsync(ct);

        if (row is null)
            await context.TenantSettings.AddAsync(new TenantSetting(key, value), ct);
        else
            row.SetValue(value);

        await context.SaveChangesAsync(ct);
    }

    public async Task<bool> RemoveAsync(string key, CancellationToken ct = default)
    {
        var row = await Query(key).FirstOrDefaultAsync(ct);
        if (row is null)
            return false;

        context.TenantSettings.Remove(row);
        await context.SaveChangesAsync(ct);
        return true;
    }

    private IQueryable<TenantSetting> Query(string key) =>
        context.TenantSettings.Where(s => s.Key == key && s.Environment == "");

    private static TenantSettingRecord ToRecord(TenantSetting row) => new(
        row.Key,
        row.Environment,
        row.Value,
        row.UpdatedAt ?? row.CreatedAt,
        row.UpdatedBy ?? row.CreatedBy);
}
