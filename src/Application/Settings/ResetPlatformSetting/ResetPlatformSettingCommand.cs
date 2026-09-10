namespace NovaFE.Application.Settings.ResetPlatformSetting;

/// <summary>Quita el override de un setting: vuelve a regir el default de código.</summary>
public sealed record ResetPlatformSettingCommand(string Key);
