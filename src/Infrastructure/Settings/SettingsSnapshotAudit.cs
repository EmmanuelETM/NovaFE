using NovaFE.Domain.Settings;

namespace NovaFE.Infrastructure.Settings;

/// <summary>
/// Revisa los overrides guardados contra el catálogo de <see cref="SettingDefinitions"/>.
/// Un override cuya definición se borró, o cuyo valor ya no parsea (la definición
/// se re-tipó), se ignora en silencio en la resolución — esto lo saca a la luz.
/// Función pura: devuelve mensajes, no loguea.
/// </summary>
internal static class SettingsSnapshotAudit
{
    public static IReadOnlyList<string> Inspect(IReadOnlyDictionary<string, string> overrides)
    {
        ArgumentNullException.ThrowIfNull(overrides);

        var issues = new List<string>();

        foreach (var (key, value) in overrides)
        {
            var definition = SettingDefinitions.FindByKey(key);

            if (definition is null)
            {
                issues.Add($"override huérfano: «{key}» ya no es un setting conocido (se ignora)");
                continue;
            }

            if (definition.Validate(value).IsError)
                issues.Add(
                    $"override inválido: «{key}» = «{value}» no parsea contra su definición (se usa el default)");
        }

        return issues;
    }
}
