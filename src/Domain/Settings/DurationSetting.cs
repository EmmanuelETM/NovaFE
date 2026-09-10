using System.Globalization;
using System.Text.RegularExpressions;
using ErrorOr;

namespace NovaFE.Domain.Settings;

/// <summary>
/// Setting de duración. Acepta el formato invariante de <see cref="TimeSpan"/>
/// (<c>hh:mm:ss</c>) y un atajo <c>&lt;n&gt;&lt;s|m|h|d&gt;</c> (<c>90s</c>, <c>5m</c>,
/// <c>6h</c>, <c>2d</c>). Persiste siempre en el formato invariante.
/// </summary>
public sealed partial class DurationSetting(
    string key,
    string group,
    string label,
    TimeSpan @default,
    TimeSpan? min = null,
    TimeSpan? max = null,
    string? description = null,
    SettingScope scope = SettingScope.Platform,
    bool killSwitch = false,
    bool sensitive = false,
    bool deprecated = false)
    : SettingDefinition<TimeSpan>(
        key, "duration", group, label, @default, description, unit: null,
        scope, killSwitch, sensitive, deprecated)
{
    public TimeSpan? Min { get; } = min;

    public TimeSpan? Max { get; } = max;

    public override string? Constraints => (Min, Max) switch
    {
        (null, null) => null,
        (not null, null) => $"≥ {Min}",
        (null, not null) => $"≤ {Max}",
        _ => $"{Min}–{Max}",
    };

    public override SettingHints? Hints => Min is null && Max is null
        ? null
        : new SettingHints(Min: Min is { } lo ? Format(lo) : null, Max: Max is { } hi ? Format(hi) : null);

    public override string Format(TimeSpan value) => value.ToString("c", CultureInfo.InvariantCulture);

    public override bool TryParse(string raw, out TimeSpan value)
    {
        value = TimeSpan.Zero;
        var trimmed = raw?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return false;

        var shorthand = ShorthandPattern().Match(trimmed);
        if (shorthand.Success)
        {
            var amount = long.Parse(shorthand.Groups[1].Value, CultureInfo.InvariantCulture);
            value = shorthand.Groups[2].Value switch
            {
                "s" => TimeSpan.FromSeconds(amount),
                "m" => TimeSpan.FromMinutes(amount),
                "h" => TimeSpan.FromHours(amount),
                "d" => TimeSpan.FromDays(amount),
                _ => TimeSpan.Zero,
            };
            return true;
        }

        return TimeSpan.TryParse(trimmed, CultureInfo.InvariantCulture, out value);
    }

    protected override ErrorOr<Success> ValidateTyped(TimeSpan value)
    {
        if (value < TimeSpan.Zero)
            return Error.Validation($"Setting.{Key}", "La duración no puede ser negativa.");

        if (Min is { } lo && value < lo)
            return Error.Validation($"Setting.{Key}", $"El mínimo es {lo}.");

        if (Max is { } hi && value > hi)
            return Error.Validation($"Setting.{Key}", $"El máximo es {hi}.");

        return Result.Success;
    }

    [GeneratedRegex(@"^(\d+)(s|m|h|d)$", RegexOptions.CultureInvariant)]
    private static partial Regex ShorthandPattern();
}
