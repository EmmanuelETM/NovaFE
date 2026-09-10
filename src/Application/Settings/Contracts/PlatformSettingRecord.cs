namespace NovaFE.Application.Settings.Contracts;

/// <summary>
/// Una fila de override de <c>platform_settings</c> tal cual está en la base. Uso
/// interno del motor (resolución y pantalla); no es lo que emite la API.
/// </summary>
public sealed record PlatformSettingRecord(
    string Key,
    string Environment,
    string Value,
    DateTimeOffset UpdatedAt,
    string? UpdatedBy);
