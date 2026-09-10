namespace NovaFE.Infrastructure.Settings;

/// <summary>
/// Foto inmutable de los overrides de settings de plataforma en un momento dado,
/// junto con la generación con que se cargó. Toda lectura de settings sale de acá
/// (sin E/S); un poller la reemplaza cuando la generación cambia.
/// </summary>
internal sealed record SettingsSnapshot(long Generation, IReadOnlyDictionary<string, string> Overrides)
{
    /// <summary>Snapshot vacío de arranque: el lector devuelve solo defaults de código.</summary>
    public static readonly SettingsSnapshot Empty =
        new(-1, new Dictionary<string, string>(StringComparer.Ordinal));

    public bool TryGetOverride(string key, out string value) => Overrides.TryGetValue(key, out value!);
}
