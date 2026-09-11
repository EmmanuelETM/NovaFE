namespace NovaFE.Application.Settings.Contracts;

/// <summary>
/// Una fila de override de <c>tenant_settings</c> tal como la lee Dapper. El
/// "quién/cuándo" sale de la última entrada de <c>tenant_setting_changes</c>.
/// </summary>
public sealed record TenantSettingRecord(
    string Key,
    string Environment,
    string Value,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy);
