namespace NovaFE.Domain.Settings;

/// <summary>
/// Pistas estructuradas para que una pantalla renderice el control adecuado: las
/// opciones de un <see cref="OptionSetting"/>, o el rango de un setting numérico o
/// de duración. Complementa <see cref="SettingDefinition.Constraints"/> (que es el
/// mismo dato en texto legible).
/// </summary>
public sealed record SettingHints(
    IReadOnlyList<string>? Options = null,
    string? Min = null,
    string? Max = null);
