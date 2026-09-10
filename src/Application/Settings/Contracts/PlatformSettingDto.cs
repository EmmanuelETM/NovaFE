namespace NovaFE.Application.Settings.Contracts;

/// <summary>
/// Un setting tal como lo ve el operador: la metadata de su definición más el
/// valor efectivo resuelto. Lo que emite <c>GET /api/v1/platform-settings</c>.
/// </summary>
public sealed record PlatformSettingDto(
    string Key,
    string Group,
    string Label,
    string? Description,
    string ValueType,
    string? Unit,
    string? Constraints,
    /// <summary>Opciones válidas (solo <c>option</c>); null en el resto.</summary>
    IReadOnlyList<string>? Options,
    /// <summary>Mínimo/máximo (numéricos y duración), ya formateados; null si no aplica.</summary>
    string? Min,
    string? Max,
    bool KillSwitch,
    bool Sensitive,
    bool Deprecated,
    string DefaultValue,
    string EffectiveValue,
    bool IsOverridden,
    /// <summary><c>default</c> o <c>platform</c>.</summary>
    string ResolvedFrom,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy);
