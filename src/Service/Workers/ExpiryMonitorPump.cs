using NovaFE.Application.Notifications;
using NovaFE.Application.Tenants.Interfaces;
using NovaFE.Service.Common;

namespace NovaFE.Service.Workers;

/// <summary>
/// Un barrido del monitor de vencimientos (RF-01.6): lista los tenants activos y
/// revisa los certificados y secuencias de cada uno en su propio scope con el
/// tenant fijado en <see cref="CurrentTenant"/> — así los repos Dapper y el
/// fan-out de webhooks quedan acotados. Multi-instancia seguro (el registro de
/// avisos usa <c>INSERT … ON CONFLICT DO NOTHING</c>).
/// </summary>
internal sealed class ExpiryMonitorPump(
    IServiceScopeFactory scopeFactory,
    ILogger<ExpiryMonitorPump> logger) : IExpiryMonitorPump
{
    public async Task<int> RunOnceAsync(CancellationToken ct = default)
    {
        IReadOnlyList<Guid> tenantIds;
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            tenantIds = await scope.ServiceProvider
                .GetRequiredService<ITenantReadRepository>()
                .ListActiveIdsAsync(ct);
        }

        var scanned = 0;
        foreach (var tenantId in tenantIds)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                scope.ServiceProvider.GetRequiredService<CurrentTenant>().Set(tenantId);

                await scope.ServiceProvider
                    .GetRequiredService<ExpiryScan>()
                    .RunForCurrentTenantAsync(tenantId, ct);

                scanned++;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "El barrido de vencimientos del tenant {TenantId} falló", tenantId);
            }
        }

        return scanned;
    }
}
