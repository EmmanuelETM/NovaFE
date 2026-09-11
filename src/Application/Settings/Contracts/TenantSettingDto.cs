namespace NovaFE.Application.Settings.Contracts;

/// <summary>
/// Un setting de scope <c>Tenant</c> tal como lo ve el contribuyente: la metadata
/// de su definición más el valor efectivo resuelto. Lo que emite
/// <c>GET /api/v1/settings</c>. Sin <c>KillSwitch</c>/<c>Sensitive</c> (no aplican
/// a settings de tenant en este slice).
/// </summary>
public sealed record TenantSettingDto(
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
    /// <summary>El contribuyente puede editarlo (self-serve).</summary>
    bool TenantWritable,
    bool Deprecated,
    string DefaultValue,
    string EffectiveValue,
    bool IsOverridden,
    /// <summary><c>default</c>, <c>platform</c> o <c>tenant</c>.</summary>
    string ResolvedFrom,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy);
