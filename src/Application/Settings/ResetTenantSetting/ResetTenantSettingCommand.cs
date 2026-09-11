namespace NovaFE.Application.Settings.ResetTenantSetting;

/// <summary>Quita el override de un setting del tenant: vuelve a regir la capa de plataforma o el default.</summary>
public sealed record ResetTenantSettingCommand(string Key);
