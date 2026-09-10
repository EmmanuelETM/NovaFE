using ErrorOr;
using NovaFE.Application.Common;
using NovaFE.Application.Settings.Contracts;
using NovaFE.Application.Settings.Interfaces;
using NovaFE.Domain.Settings;
using Microsoft.Extensions.Logging;

namespace NovaFE.Application.Settings.ListPlatformSettings;

/// <summary>
/// Lista todos los settings de plataforma: recorre las <b>definiciones</b> (no las
/// filas), así un setting recién declarado aparece con su default y una fila
/// huérfana de una definición borrada se ignora.
/// </summary>
public sealed class ListPlatformSettingsUseCase(
    ILoggerFactory loggerFactory,
    IPlatformSettingReadRepository readRepository)
    : ParameterlessQueryUseCase<IReadOnlyList<PlatformSettingDto>>(loggerFactory)
{
    protected override async Task<ErrorOr<IReadOnlyList<PlatformSettingDto>>> ExecuteCore(
        NoRequest request, CancellationToken ct)
    {
        var rows = await readRepository.ListAsync(ct);
        var byKey = rows
            .Where(r => r.Environment.Length == 0)
            .ToDictionary(r => r.Key, r => r, StringComparer.Ordinal);

        return SettingDefinitions.All
            .Select(def => PlatformSettingMapper.ToDto(def, byKey.GetValueOrDefault(def.Key)))
            .ToArray();
    }
}
