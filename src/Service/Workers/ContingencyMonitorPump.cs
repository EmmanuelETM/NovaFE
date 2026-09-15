using NovaFE.Application.Common.Interfaces;
using NovaFE.Application.Contingency;
using NovaFE.Application.Tenants.Interfaces;
using NovaFE.Application.Webhooks;
using NovaFE.Application.Webhooks.Interfaces;
using NovaFE.Service.Common;
using NovaFE.Service.Configuration;
using Microsoft.Extensions.Options;

namespace NovaFE.Service.Workers;

/// <summary>
/// Un tick del monitor de contingencia (M11 Tipo 1): revisa el outbox de envío a
/// la DGII en un scope sin tenant (es una señal de plataforma, igual que la
/// consola de operación), y si <see cref="ContingencyMonitor"/> detecta una
/// transición, avisa a cada tenant activo suscrito — mismo patrón de loop por
/// tenant que <see cref="ExpiryMonitorPump"/>, porque el evento es de plataforma y
/// todos los suscritos deben enterarse, no solo el que tuvo la mala suerte de
/// intentar un envío primero.
/// </summary>
internal sealed class ContingencyMonitorPump(
    IServiceScopeFactory scopeFactory,
    ILogger<ContingencyMonitorPump> logger) : IContingencyMonitorPump
{
    public async Task<ContingencyTransition?> RunOnceAsync(CancellationToken ct = default)
    {
        ContingencyTransition? transition;
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var threshold = scope.ServiceProvider
                .GetRequiredService<IOptionsMonitor<ContingencyMonitorOptions>>().CurrentValue.ActivationThreshold;

            transition = await scope.ServiceProvider
                .GetRequiredService<ContingencyMonitor>()
                .CheckAndTransitionAsync(threshold, ct);
        }

        if (transition is null)
            return null;

        IReadOnlyList<Guid> tenantIds;
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            tenantIds = await scope.ServiceProvider
                .GetRequiredService<ITenantReadRepository>()
                .ListActiveIdsAsync(ct);
        }

        foreach (var tenantId in tenantIds)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                scope.ServiceProvider.GetRequiredService<CurrentTenant>().Set(tenantId);

                var webhookOutbox = scope.ServiceProvider.GetRequiredService<IWebhookOutbox>();
                var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

                await unitOfWork.ExecuteInTransactionAsync(
                    t => webhookOutbox.EnqueueAsync(
                        WebhookEvent.Create(transition.EventType, transition.Payload, transition.Payload.ChangedAt),
                        tenantId, t),
                    ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "No se pudo avisar al tenant {TenantId} de {EventType}", tenantId, transition.EventType);
            }
        }

        return transition;
    }
}
