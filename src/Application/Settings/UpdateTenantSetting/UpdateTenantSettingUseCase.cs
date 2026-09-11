using ErrorOr;
using FluentValidation;
using NovaFE.Application.Common;
using NovaFE.Application.Common.Interfaces;
using NovaFE.Application.Settings.Contracts;
using NovaFE.Application.Settings.Interfaces;
using NovaFE.Domain.Common;
using NovaFE.Domain.Settings;
using Microsoft.Extensions.Logging;

namespace NovaFE.Application.Settings.UpdateTenantSetting;

/// <summary>
/// Sobrescribe un setting para el tenant actual: valida contra la definición,
/// persiste la fila + la bitácora en una transacción. No toca el contador de
/// generación — los valores de tenant no están cacheados en el snapshot global
/// (ver <c>docs/configuration.md</c>).
/// </summary>
public sealed class UpdateTenantSettingUseCase(
    ILoggerFactory loggerFactory,
    IValidator<UpdateTenantSettingCommand> validator,
    ICurrentTenant currentTenant,
    ITenantSettingRepository repository,
    ITenantSettingChangeLog changeLog,
    IUnitOfWork unitOfWork)
    : CommandUseCase<UpdateTenantSettingCommand, TenantSettingDto>(loggerFactory, validator)
{
    protected override async Task<ErrorOr<TenantSettingDto>> ExecuteCore(
        UpdateTenantSettingCommand request, CancellationToken ct)
    {
        if (currentTenant.TenantId is null)
            return Errors.Auth.TenantNotResolved;

        var definition = TenantSettingCatalog.Resolve(request.Key);
        if (definition is null)
            return SettingErrors.UnknownKey(request.Key);

        if (definition.Deprecated)
            return SettingErrors.Deprecated(request.Key);

        var validation = definition.Validate(request.Value);
        if (validation.IsError)
            return validation.Errors;

        var value = definition.Canonicalize(request.Value);
        var previous = (await repository.GetAsync(definition.Key, ct))?.Value;

        if (previous == value)
            return await BuildDtoAsync(definition, ct);

        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            await repository.UpsertAsync(definition.Key, value, token);
            await changeLog.RecordAsync(definition.Key, previous, value, token);
        }, ct);

        return await BuildDtoAsync(definition, ct);
    }

    private async Task<TenantSettingDto> BuildDtoAsync(SettingDefinition definition, CancellationToken ct)
        => TenantSettingMapper.ToDto(
            definition, await repository.GetAsync(definition.Key, ct), platformOverrideValue: null);
}
