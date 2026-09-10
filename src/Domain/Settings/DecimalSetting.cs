using System.Globalization;
using ErrorOr;

namespace NovaFE.Domain.Settings;

/// <summary>Setting decimal, con rango opcional <c>[Min, Max]</c>. Punto como separador.</summary>
public sealed class DecimalSetting(
    string key,
    string group,
    string label,
    decimal @default,
    decimal? min = null,
    decimal? max = null,
    string? unit = null,
    string? description = null,
    SettingScope scope = SettingScope.Platform,
    bool killSwitch = false,
    bool sensitive = false,
    bool deprecated = false)
    : SettingDefinition<decimal>(
        key, "decimal", group, label, @default, description, unit,
        scope, killSwitch, sensitive, deprecated)
{
    public decimal? Min { get; } = min;

    public decimal? Max { get; } = max;

    public override string? Constraints => (Min, Max) switch
    {
        (null, null) => null,
        (not null, null) => $"≥ {Min}",
        (null, not null) => $"≤ {Max}",
        _ => $"{Min}–{Max}",
    };

    public override SettingHints? Hints => Min is null && Max is null
        ? null
        : new SettingHints(
            Min: Min?.ToString(CultureInfo.InvariantCulture),
            Max: Max?.ToString(CultureInfo.InvariantCulture));

    public override string Format(decimal value) => value.ToString(CultureInfo.InvariantCulture);

    public override bool TryParse(string raw, out decimal value) =>
        decimal.TryParse(raw?.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out value);

    protected override ErrorOr<Success> ValidateTyped(decimal value)
    {
        if (Min is { } lo && value < lo)
            return Error.Validation($"Setting.{Key}", $"El mínimo es {lo}.");

        if (Max is { } hi && value > hi)
            return Error.Validation($"Setting.{Key}", $"El máximo es {hi}.");

        return Result.Success;
    }
}
