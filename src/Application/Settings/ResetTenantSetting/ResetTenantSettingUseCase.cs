using ErrorOr;
using NovaFE.Application.Common;
using NovaFE.Application.Common.Interfaces;
using NovaFE.Application.Settings.Contracts;
using NovaFE.Application.Settings.Interfaces;
using NovaFE.Domain.Common;
using NovaFE.Domain.Settings;
using Microsoft.Extensions.Logging;

namespace NovaFE.Application.Settings.ResetTenantSetting;

/// <summary>
/// Quita el override de un setting del tenant. Si no había, es un no-op idempotente.
/// Si lo había, borra la fila + registra el cambio con <c>new_value = null</c> en
/// una transacción. El valor efectivo vuelve a la capa de plataforma o al default.
/// </summary>
public sealed class ResetTenantSettingUseCase(
    ILoggerFactory loggerFactory,
    ICurrentTenant currentTenant,
    ITenantSettingRepository repository,
    ITenantSettingChangeLog changeLog,
    IPlatformSettingRepository platformSettings,
    IUnitOfWork unitOfWork)
    : CommandUseCase<ResetTenantSettingCommand, TenantSettingDto>(loggerFactory)
{
    protected override async Task<ErrorOr<TenantSettingDto>> ExecuteCore(
        ResetTenantSettingCommand request, CancellationToken ct)
    {
        if (currentTenant.TenantId is null)
            return Errors.Auth.TenantNotResolved;

        var definition = TenantSettingCatalog.Resolve(request.Key);
        if (definition is null)
            return SettingErrors.UnknownKey(request.Key);

        var previous = (await repository.GetAsync(definition.Key, ct))?.Value;

        if (previous is not null)
        {
            await unitOfWork.ExecuteInTransactionAsync(async token =>
            {
                await repository.RemoveAsync(definition.Key, token);
                await changeLog.RecordAsync(definition.Key, previous, newValue: null, token);
            }, ct);
        }

        var platformValue = (await platformSettings.GetAsync(definition.Key, ct))?.Value;
        return TenantSettingMapper.ToDto(definition, tenantRow: null, platformValue);
    }
}
