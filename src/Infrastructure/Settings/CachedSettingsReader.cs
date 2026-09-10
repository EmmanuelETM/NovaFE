using NovaFE.Application.Settings.Interfaces;
using NovaFE.Domain.Settings;
using Microsoft.Extensions.Logging;

namespace NovaFE.Infrastructure.Settings;

/// <summary>
/// <see cref="ISettingsReader"/> sobre el snapshot en memoria. Singleton, sin E/S.
/// Un override que no parsea contra su definición <b>no lanza</b>: se registra y se
/// devuelve el default de código.
/// </summary>
internal sealed class CachedSettingsReader(
    ISettingsSnapshotHolder holder,
    ILogger<CachedSettingsReader> logger) : ISettingsReader
{
    public T GetValue<T>(SettingDefinition<T> definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (holder.Current.TryGetOverride(definition.Key, out var raw))
        {
            if (definition.TryParse(raw, out var value))
                return value;

            logger.LogWarning(
                "Setting {Key}: el override «{Raw}» no parsea como {Type}; se usa el default",
                definition.Key, raw, definition.ValueType);
        }

        return definition.Default;
    }
}
