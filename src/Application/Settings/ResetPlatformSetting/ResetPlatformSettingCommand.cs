namespace NovaFE.Application.Settings.ResetPlatformSetting;

/// <summary>
/// Quita el override de un setting: vuelve a regir el default de código.
/// <see cref="Confirmed"/> lo exige un setting <c>Sensitive</c>.
/// </summary>
public sealed record ResetPlatformSettingCommand(string Key, bool Confirmed = false);
