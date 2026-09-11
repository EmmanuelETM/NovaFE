namespace NovaFE.Application.Settings.UpdatePlatformSetting;

/// <summary>
/// Sobrescribe (crea o reemplaza) el override de un setting de plataforma. La
/// clave viene de la ruta; el valor, del cuerpo. <see cref="Confirmed"/> lo exige
/// un setting <c>Sensitive</c>.
/// </summary>
public sealed record UpdatePlatformSettingCommand(string Key, string Value, bool Confirmed = false);
