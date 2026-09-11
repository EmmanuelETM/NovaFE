using ErrorOr;
using NovaFE.Application.Common;
using NovaFE.Application.Common.Interfaces;
using NovaFE.Application.Settings.Contracts;
using NovaFE.Application.Settings.Interfaces;
using NovaFE.Domain.Settings;
using Microsoft.Extensions.Logging;

namespace NovaFE.Application.Settings.ResetPlatformSetting;

/// <summary>
/// Quita el override de un setting. Si no había, es un no-op idempotente (devuelve
/// el default). Si lo había, borra la fila + registra el cambio con
/// <c>new_value = null</c> + bump + recarga el snapshot.
/// </summary>
public sealed class ResetPlatformSettingUseCase(
    ILoggerFactory loggerFactory,
    IPlatformSettingRepository repository,
    IPlatformSettingChangeLog changeLog,
    ISettingsGenerationStore generationStore,
    ISettingsCacheInvalidator cacheInvalidator,
    IUnitOfWork unitOfWork)
    : CommandUseCase<ResetPlatformSettingCommand, PlatformSettingDto>(loggerFactory)
{
    protected override async Task<ErrorOr<PlatformSettingDto>> ExecuteCore(
        ResetPlatformSettingCommand request, CancellationToken ct)
    {
        var definition = SettingDefinitions.FindByKey(request.Key);
        if (definition is null)
            return SettingErrors.UnknownKey(request.Key);

        if (definition.Sensitive && !request.Confirmed)
            return SettingErrors.ConfirmationRequired(request.Key);

        var previous = (await repository.GetAsync(definition.Key, ct))?.Value;
        if (previous is null)
            return PlatformSettingMapper.ToDto(definition, overrideRow: null);

        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            await repository.RemoveAsync(definition.Key, token);
            await changeLog.RecordAsync(definition.Key, previous, newValue: null, token);
            await generationStore.BumpAsync(token);
        }, ct);

        await cacheInvalidator.RefreshNowAsync(ct);

        return PlatformSettingMapper.ToDto(definition, overrideRow: null);
    }
}
