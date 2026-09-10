using System.Globalization;
using ErrorOr;

namespace NovaFE.Domain.Settings;

/// <summary>Setting entero, con rango opcional <c>[Min, Max]</c> (ambos inclusivos).</summary>
public sealed class IntegerSetting(
    string key,
    string group,
    string label,
    int @default,
    int? min = null,
    int? max = null,
    string? unit = null,
    string? description = null,
    SettingScope scope = SettingScope.Platform,
    bool killSwitch = false,
    bool sensitive = false,
    bool deprecated = false)
    : SettingDefinition<int>(
        key, "integer", group, label, @default, description, unit,
        scope, killSwitch, sensitive, deprecated)
{
    public int? Min { get; } = min;

    public int? Max { get; } = max;

    public override string? Constraints => (Min, Max) switch
    {
        (null, null) => null,
        (not null, null) => $"≥ {Min}",
        (null, not null) => $"≤ {Max}",
        _ => $"{Min}–{Max}",
    };

    public override string Format(int value) => value.ToString(CultureInfo.InvariantCulture);

    public override bool TryParse(string raw, out int value) =>
        int.TryParse(raw?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    protected override ErrorOr<Success> ValidateTyped(int value)
    {
        if (Min is { } lo && value < lo)
            return Error.Validation($"Setting.{Key}", $"El mínimo es {lo}.");

        if (Max is { } hi && value > hi)
            return Error.Validation($"Setting.{Key}", $"El máximo es {hi}.");

        return Result.Success;
    }
}
