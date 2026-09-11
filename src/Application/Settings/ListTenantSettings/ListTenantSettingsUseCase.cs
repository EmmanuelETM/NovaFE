using ErrorOr;
using NovaFE.Application.Common;
using NovaFE.Application.Common.Interfaces;
using NovaFE.Application.Settings.Contracts;
using NovaFE.Application.Settings.Interfaces;
using NovaFE.Domain.Common;
using NovaFE.Domain.Settings;
using Microsoft.Extensions.Logging;

namespace NovaFE.Application.Settings.ListTenantSettings;

/// <summary>
/// Lista los settings que el contribuyente puede ajustar: recorre las
/// <b>definiciones</b> de scope <see cref="SettingScope.Tenant"/> con
/// <c>TenantWritable</c>, y por cada una resuelve el valor efectivo
/// <c>tenant → platform → default</c>.
/// </summary>
public sealed class ListTenantSettingsUseCase(
    ILoggerFactory loggerFactory,
    ICurrentTenant currentTenant,
    ITenantSettingReadRepository tenantSettings,
    IPlatformSettingReadRepository platformSettings)
    : ParameterlessQueryUseCase<IReadOnlyList<TenantSettingDto>>(loggerFactory)
{
    protected override async Task<ErrorOr<IReadOnlyList<TenantSettingDto>>> ExecuteCore(
        NoRequest request, CancellationToken ct)
    {
        if (currentTenant.TenantId is not { } tenantId)
            return Errors.Auth.TenantNotResolved;

        var tenantRows = (await tenantSettings.ListAsync(tenantId, ct))
            .Where(r => r.Environment.Length == 0)
            .ToDictionary(r => r.Key, r => r, StringComparer.Ordinal);

        var platformRows = (await platformSettings.ListAsync(ct))
            .Where(r => r.Environment.Length == 0)
            .ToDictionary(r => r.Key, r => r.Value, StringComparer.Ordinal);

        return SettingDefinitions.All
            .Where(d => d.Scope == SettingScope.Tenant && d.TenantWritable && !d.Deprecated)
            .Select(def => TenantSettingMapper.ToDto(
                def,
                tenantRows.GetValueOrDefault(def.Key),
                platformRows.GetValueOrDefault(def.Key)))
            .ToArray();
    }
}
