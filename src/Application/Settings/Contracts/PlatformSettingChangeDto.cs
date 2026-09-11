namespace NovaFE.Application.Settings.Contracts;

/// <summary>
/// Una entrada de la bitácora de cambios de un setting de plataforma
/// (<c>platform_setting_changes</c>). <c>PreviousValue</c> null = primera
/// sobrescritura; <c>NewValue</c> null = vuelta al default.
/// </summary>
public sealed record PlatformSettingChangeDto(
    string? PreviousValue,
    string? NewValue,
    DateTimeOffset ChangedAt,
    string? ChangedBy);
