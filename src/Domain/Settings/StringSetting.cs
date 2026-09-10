using System.Text.RegularExpressions;
using ErrorOr;

namespace NovaFE.Domain.Settings;

/// <summary>Setting de texto libre, con largo máximo y patrón opcionales.</summary>
public sealed class StringSetting(
    string key,
    string group,
    string label,
    string @default,
    int? maxLength = null,
    string? pattern = null,
    bool allowEmpty = true,
    string? description = null,
    SettingScope scope = SettingScope.Platform,
    bool killSwitch = false,
    bool sensitive = false,
    bool deprecated = false)
    : SettingDefinition<string>(
        key, "string", group, label, @default, description, unit: null,
        scope, killSwitch, sensitive, deprecated)
{
    private readonly Regex? _pattern = pattern is null
        ? null
        : new Regex(pattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    public int? MaxLength { get; } = maxLength;

    public string? Pattern { get; } = pattern;

    public bool AllowEmpty { get; } = allowEmpty;

    public override string? Constraints
    {
        get
        {
            var parts = new List<string>();
            if (MaxLength is { } max)
                parts.Add($"máx. {max} caracteres");
            if (Pattern is not null)
                parts.Add($"patrón {Pattern}");
            return parts.Count == 0 ? null : string.Join("; ", parts);
        }
    }

    public override string Format(string value) => value ?? string.Empty;

    public override bool TryParse(string raw, out string value)
    {
        value = raw ?? string.Empty;
        return raw is not null;
    }

    protected override ErrorOr<Success> ValidateTyped(string value)
    {
        if (!AllowEmpty && string.IsNullOrEmpty(value))
            return Error.Validation($"Setting.{Key}", "El valor no puede estar vacío.");

        if (MaxLength is { } max && value.Length > max)
            return Error.Validation($"Setting.{Key}", $"El largo máximo es {max} caracteres.");

        if (_pattern is not null && value.Length > 0 && !_pattern.IsMatch(value))
            return Error.Validation($"Setting.{Key}", $"El valor no cumple el patrón {Pattern}.");

        return Result.Success;
    }
}
