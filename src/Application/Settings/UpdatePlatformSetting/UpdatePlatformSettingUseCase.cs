using ErrorOr;
using FluentValidation;
using NovaFE.Application.Common;
using NovaFE.Application.Common.Interfaces;
using NovaFE.Application.Settings.Contracts;
using NovaFE.Application.Settings.Interfaces;
using NovaFE.Domain.Settings;
using Microsoft.Extensions.Logging;

namespace NovaFE.Application.Settings.UpdatePlatformSetting;

/// <summary>
/// Sobrescribe un setting de plataforma: valida contra la definición, persiste la
/// fila + la bitácora + el bump de generación en una transacción, y recarga el
/// snapshot local para que esta instancia lo vea al instante.
/// </summary>
public sealed class UpdatePlatformSettingUseCase(
    ILoggerFactory loggerFactory,
    IValidator<UpdatePlatformSettingCommand> validator,
    IPlatformSettingRepository repository,
    IPlatformSettingChangeLog changeLog,
    ISettingsGenerationStore generationStore,
    ISettingsCacheInvalidator cacheInvalidator,
    IUnitOfWork unitOfWork)
    : CommandUseCase<UpdatePlatformSettingCommand, PlatformSettingDto>(loggerFactory, validator)
{
    protected override async Task<ErrorOr<PlatformSettingDto>> ExecuteCore(
        UpdatePlatformSettingCommand request, CancellationToken ct)
    {
        var definition = SettingDefinitions.FindByKey(request.Key);
        if (definition is null)
            return SettingErrors.UnknownKey(request.Key);

        if (definition.Deprecated)
            return SettingErrors.Deprecated(request.Key);

        if (definition.Sensitive && !request.Confirmed)
            return SettingErrors.ConfirmationRequired(request.Key);

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
            await generationStore.BumpAsync(token);
        }, ct);

        await cacheInvalidator.RefreshNowAsync(ct);

        return await BuildDtoAsync(definition, ct);
    }

    private async Task<PlatformSettingDto> BuildDtoAsync(SettingDefinition definition, CancellationToken ct)
        => PlatformSettingMapper.ToDto(definition, await repository.GetAsync(definition.Key, ct));
}
