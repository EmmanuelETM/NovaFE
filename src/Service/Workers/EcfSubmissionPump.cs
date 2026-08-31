using Microsoft.Extensions.Options;
using NovaFE.Application.Ecf.Interfaces;
using NovaFE.Application.Ecf.Submission;
using NovaFE.Service.Common;
using NovaFE.Service.Configuration;

namespace NovaFE.Service.Workers;

/// <summary>
/// Un tick del envío a la DGII: recupera filas atascadas, reclama un lote (sin
/// tenant, la tabla de outbox no lleva RLS) y procesa cada fila en su propio scope
/// con el tenant de la fila fijado en <see cref="CurrentTenant"/> — así el
/// repositorio y el token de la DGII quedan acotados al tenant correcto.
/// </summary>
internal sealed class EcfSubmissionPump(
    IServiceScopeFactory scopeFactory,
    IOptions<EcfSubmissionOptions> options,
    ILogger<EcfSubmissionPump> logger) : IEcfSubmissionPump
{
    public async Task<int> RunOnceAsync(CancellationToken ct = default)
    {
        var opts = options.Value;

        using var claimScope = scopeFactory.CreateScope();
        var queue = claimScope.ServiceProvider.GetRequiredService<IEcfSubmissionQueue>();

        var reaped = await queue.ReapStuckAsync(opts.StuckAfter, ct);
        if (reaped > 0)
            logger.LogInformation("Recuperadas {Count} filas de envío atascadas", reaped);

        var batch = await queue.ClaimBatchAsync(opts.BatchSize, ct);

        var processed = 0;
        foreach (var item in batch)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                using var itemScope = scopeFactory.CreateScope();
                itemScope.ServiceProvider.GetRequiredService<CurrentTenant>().Set(item.TenantId);

                await itemScope.ServiceProvider
                    .GetRequiredService<EcfSubmissionProcessor>()
                    .ProcessAsync(item, ct);

                processed++;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Falló el procesamiento de la fila de envío {RowId} (e-CF {EcfId})", item.Id, item.EcfId);
            }
        }

        return processed;
    }
}
