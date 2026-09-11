using NovaFE.Application.Common.Interfaces;
using NovaFE.Application.Settings.Interfaces;
using NovaFE.Domain.Settings;
using Microsoft.Extensions.Logging;

namespace NovaFE.Infrastructure.Settings;

/// <summary>
/// <see cref="ITenantSettingsReader"/> con vida de scope. La primera lectura trae
/// las filas de <c>tenant_settings</c> del tenant actual (dentro del scope de la
/// petición, donde RLS/el filtro de EF ya acotan) y las memoriza. Resolución
/// <c>tenant → platform → default</c>: si no hay fila de tenant, delega en
/// <see cref="ISettingsReader"/> (snapshot de plataforma → default de código).
/// Un override corrupto no lanza: se registra y se cae a la capa siguiente.
/// </summary>
internal sealed class ScopedTenantSettingsReader(
    ICurrentTenant currentTenant,
    ITenantSettingReadRepository readRepository,
    ISettingsReader platformReader,
    ILogger<ScopedTenantSettingsReader> logger) : ITenantSettingsReader
{
    private Dictionary<string, string>? _overrides;

    public async Task<T> GetValueAsync<T>(SettingDefinition<T> definition, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (currentTenant.TenantId is not { } tenantId)
            return platformReader.GetValue(definition);

        _overrides ??= await LoadAsync(tenantId, ct);

        if (_overrides.TryGetValue(definition.Key, out var raw))
        {
            if (definition.TryParse(raw, out var value))
                return value;

            logger.LogWarning(
                "Tenant setting {Key}: el override «{Raw}» no parsea como {Type}; se usa la capa de plataforma",
                definition.Key, raw, definition.ValueType);
        }

        return platformReader.GetValue(definition);
    }

    private async Task<Dictionary<string, string>> LoadAsync(Guid tenantId, CancellationToken ct)
    {
        var rows = await readRepository.ListAsync(tenantId, ct);

        return rows
            .Where(r => r.Environment.Length == 0)
            .ToDictionary(r => r.Key, r => r.Value, StringComparer.Ordinal);
    }
}
