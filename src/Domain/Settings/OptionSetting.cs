using ErrorOr;

namespace NovaFE.Domain.Settings;

/// <summary>
/// Setting con un conjunto cerrado de opciones (una lista tipo enum). El valor es
/// una de <see cref="Options"/>; la comparación no distingue mayúsculas y se
/// persiste con la forma canónica declarada.
/// </summary>
public sealed class OptionSetting : SettingDefinition<string>
{
    public OptionSetting(
        string key,
        string group,
        string label,
        string @default,
        IReadOnlyList<string> options,
        string? description = null,
        SettingScope scope = SettingScope.Platform,
        bool killSwitch = false,
        bool sensitive = false,
        bool deprecated = false)
        : base(key, "option", group, label, @default, description, unit: null,
            scope, killSwitch, sensitive, deprecated)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Count == 0)
            throw new ArgumentException("Un OptionSetting necesita al menos una opción.", nameof(options));

        Options = options;

        if (!TryParse(@default, out _))
            throw new ArgumentException(
                $"El default «{@default}» no está entre las opciones de {key}.", nameof(@default));
    }

    public IReadOnlyList<string> Options { get; }

    public override string? Constraints => string.Join(" · ", Options);

    public override string Format(string value) => value ?? string.Empty;

    public override bool TryParse(string raw, out string value)
    {
        var trimmed = raw?.Trim() ?? string.Empty;
        var match = Options.FirstOrDefault(
            o => string.Equals(o, trimmed, StringComparison.OrdinalIgnoreCase));

        value = match ?? string.Empty;
        return match is not null;
    }

    protected override ErrorOr<Success> ValidateTyped(string value)
        => Options.Contains(value)
            ? Result.Success
            : Error.Validation($"Setting.{Key}", $"Opciones válidas: {string.Join(", ", Options)}.");
}
