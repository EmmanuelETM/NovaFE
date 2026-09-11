using ErrorOr;
using NovaFE.Application.Common;
using NovaFE.Application.Settings.Contracts;
using NovaFE.Application.Settings.Interfaces;
using NovaFE.Domain.Settings;
using Microsoft.Extensions.Logging;

namespace NovaFE.Application.Settings.GetPlatformSettingHistory;

/// <summary>La bitácora de cambios de un setting. Recurso de operador.</summary>
public sealed class GetPlatformSettingHistoryUseCase(
    ILoggerFactory loggerFactory,
    IPlatformSettingChangeReadRepository changes)
    : QueryUseCase<GetPlatformSettingHistoryQuery, IReadOnlyList<PlatformSettingChangeDto>>(loggerFactory)
{
    protected override async Task<ErrorOr<IReadOnlyList<PlatformSettingChangeDto>>> ExecuteCore(
        GetPlatformSettingHistoryQuery request, CancellationToken ct)
    {
        if (SettingDefinitions.FindByKey(request.Key) is null)
            return SettingErrors.UnknownKey(request.Key);

        return ErrorOrFactory.From(await changes.ListByKeyAsync(request.Key, ct));
    }
}
