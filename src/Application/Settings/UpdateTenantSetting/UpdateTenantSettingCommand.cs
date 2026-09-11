namespace NovaFE.Application.Settings.UpdateTenantSetting;

/// <summary>
/// Sobrescribe (crea o reemplaza) el override de un setting para el tenant de la
/// petición. La clave viene de la ruta; el valor, del cuerpo.
/// </summary>
public sealed record UpdateTenantSettingCommand(string Key, string Value);
