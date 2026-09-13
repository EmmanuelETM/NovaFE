using NovaFE.Domain.Settings;

namespace NovaFE.Service.Configuration;

/// <summary>
/// Interpreta el valor de un <c>StringSetting</c> tipo "ladder" (lista separada
/// por comas de atajos de duración, p. ej. <c>"5m,30m,30m"</c>) como
/// <see cref="TimeSpan"/>. Un token que no parsea cae al <paramref name="fallback"/>
/// completo — igual criterio que <c>CachedSettingsReader</c>: un override corrupto
/// nunca lanza, se ignora entero en vez de devolver una lista a medias.
/// </summary>
internal static class DurationLadder
{
    public static IReadOnlyList<TimeSpan> Parse(string raw, IReadOnlyList<TimeSpan> fallback)
    {
        var parts = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
            return fallback;

        var result = new List<TimeSpan>(parts.Length);
        foreach (var part in parts)
        {
            if (!DurationSetting.TryParseAny(part, out var value))
                return fallback;

            result.Add(value);
        }

        return result;
    }
}
