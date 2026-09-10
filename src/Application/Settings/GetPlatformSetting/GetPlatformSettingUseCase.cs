using ErrorOr;
using NovaFE.Application.Common;
using NovaFE.Application.Settings.Contracts;
using NovaFE.Application.Settings.Interfaces;
using NovaFE.Domain.Settings;
using Microsoft.Extensions.Logging;

namespace NovaFE.Application.Settings.GetPlatformSetting;

public sealed class GetPlatformSettingUseCase(
    ILoggerFactory loggerFactory,
    IPlatformSettingRepository repository)
    : QueryUseCase<GetPlatformSettingQuery, PlatformSettingDto>(loggerFactory)
{
    protected override async Task<ErrorOr<PlatformSettingDto>> ExecuteCore(
        GetPlatformSettingQuery request, CancellationToken ct)
    {
        var definition = SettingDefinitions.FindByKey(request.Key);
        if (definition is null)
            return SettingErrors.UnknownKey(request.Key);

        return PlatformSettingMapper.ToDto(definition, await repository.GetAsync(definition.Key, ct));
    }
}
