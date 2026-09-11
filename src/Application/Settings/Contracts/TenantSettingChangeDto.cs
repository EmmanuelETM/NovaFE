namespace NovaFE.Application.Settings.Contracts;

/// <summary>
/// Una entrada de la bitácora de un setting de tenant. <c>PreviousValue = null</c>
/// es la primera sobrescritura; <c>NewValue = null</c> es la vuelta al default.
/// </summary>
public sealed record TenantSettingChangeDto(
    string? PreviousValue,
    string? NewValue,
    DateTimeOffset ChangedAt,
    string? ChangedBy);
