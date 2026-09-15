using Microsoft.Extensions.Logging;
using NovaFE.Application.Common;
using NovaFE.Application.Ops.Interfaces;
using NovaFE.Application.Settings.Contracts;
using NovaFE.Application.Settings.Interfaces;
using NovaFE.Application.Settings.UpdatePlatformSetting;
using NovaFE.Domain.Settings;
using NovaFE.Domain.Webhooks;

namespace NovaFE.Application.Contingency;

/// <summary>
/// Detecta y aplica la transición de <c>platform.contingency_mode</c> (M11 Tipo 1):
/// activa cuando el outbox de envío a la DGII lleva demasiado tiempo estancado,
/// desactiva cuando se pone al día. Reusa <see cref="IOpsStatusReadRepository"/>
/// (la misma señal que ya alimenta la consola de operación) en vez de engancharse
/// al circuit breaker — es Postgres-backed y ya funciona multi-instancia.
/// <para>
/// Escribe el setting con <see cref="UpdatePlatformSettingUseCase"/> directamente
/// (Application, resoluble por DI, sin HTTP de por medio) con <c>Confirmed: true</c>
/// — es el único punto donde se exige la confirmación de un setting <c>Sensitive</c>,
/// y un detector automático no tiene una pantalla donde pedirla. El mismo setting
/// lo puede seguir prendiendo/apagando un operador a mano
/// (<c>PUT /api/v1/platform-settings/platform.contingency_mode</c>) — ambos caminos
/// escriben el mismo valor, no hay un flag separado por origen.
/// </para>
/// Servicio plano (no <c>IUseCase</c>), igual que <c>EcfSubmissionProcessor</c>.
/// </summary>
public sealed class ContingencyMonitor(
    IOpsStatusReadRepository outboxStatus,
    ISettingsReader settingsReader,
    IUseCase<UpdatePlatformSettingCommand, PlatformSettingDto> updateSetting,
    TimeProvider timeProvider,
    ILogger<ContingencyMonitor> logger)
{
    /// <summary>
    /// Revisa el outbox y, si el estado real (estancado / al día) difiere del
    /// setting actual, lo corrige y devuelve la transición para que el llamador
    /// avise a los tenants suscritos. <c>null</c> si no hubo cambio.
    /// </summary>
    public async Task<ContingencyTransition?> CheckAndTransitionAsync(
        TimeSpan activationThreshold, CancellationToken ct = default)
    {
        var status = await outboxStatus.GetEcfSubmissionOutboxStatusAsync(ct);
        var now = timeProvider.GetUtcNow();
        var oldestAge = status.OldestPendingAt is { } oldest ? now - oldest : (TimeSpan?)null;
        var stale = oldestAge is { } age && age >= activationThreshold;

        var active = settingsReader.GetValue(SettingDefinitions.ContingencyMode);
        if (stale == active)
            return null;

        var result = await updateSetting.Execute(
            new UpdatePlatformSettingCommand(
                SettingDefinitions.ContingencyMode.Key, stale ? "true" : "false", Confirmed: true),
            ct);

        if (result.IsError)
        {
            logger.LogError(
                "No se pudo {Action} platform.contingency_mode: {Error}",
                stale ? "activar" : "desactivar", result.FirstError.Description);
            return null;
        }

        var minutes = oldestAge?.TotalMinutes;

        if (stale)
            logger.LogWarning(
                "Contingencia M11 Tipo 1 activada: el outbox de envío lleva {Minutes:F0} min sin resolver", minutes);
        else
            logger.LogInformation("Contingencia M11 Tipo 1 desactivada: el outbox de envío se puso al día");

        return new ContingencyTransition(
            stale ? WebhookEventType.ContingencyActivated : WebhookEventType.ContingencyDeactivated,
            new ContingencyStatusPayload(stale ? "active" : "inactive", now, minutes));
    }
}
