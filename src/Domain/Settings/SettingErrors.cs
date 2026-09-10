using ErrorOr;

namespace NovaFE.Domain.Settings;

/// <summary>
/// Errores de negocio de los settings de plataforma. <c>code</c> en inglés
/// (estable); descripción en español (la consume el operador vía la API).
/// </summary>
public static class SettingErrors
{
    public static Error UnknownKey(string key) => Error.NotFound(
        code: "Setting.UnknownKey",
        description: $"No existe un setting con la clave «{key}».");

    public static Error Deprecated(string key) => Error.Validation(
        code: "Setting.Deprecated",
        description: $"El setting «{key}» está obsoleto y no admite nuevos valores.");

    public static Error InvalidValue(string key, string reason) => Error.Validation(
        code: $"Setting.{key}",
        description: reason);
}
