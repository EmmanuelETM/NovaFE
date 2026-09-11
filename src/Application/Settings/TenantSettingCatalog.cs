using NovaFE.Domain.Settings;

namespace NovaFE.Application.Settings;

/// <summary>
/// Resuelve la definición de un setting que el contribuyente puede administrar:
/// tiene que existir, ser de scope <see cref="SettingScope.Tenant"/> y
/// <c>TenantWritable</c>. Cualquier otra clave se trata como inexistente (404) —
/// no se filtra que un setting de plataforma existe.
/// </summary>
internal static class TenantSettingCatalog
{
    public static SettingDefinition? Resolve(string key)
    {
        var definition = SettingDefinitions.FindByKey(key);

        return definition is { Scope: SettingScope.Tenant, TenantWritable: true }
            ? definition
            : null;
    }
}
