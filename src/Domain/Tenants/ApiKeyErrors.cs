using ErrorOr;

namespace NovaFE.Domain.Tenants;

/// <summary>
/// Errores de negocio de las API keys. <c>code</c> en inglés (estable); la
/// descripción la consume quien llama a la API, por eso va en español.
/// </summary>
public static class ApiKeyErrors
{
    public static Error TenantRequired => Error.Validation(
        code: "ApiKey.TenantRequired",
        description: "La credencial debe pertenecer a un contribuyente.");

    public static Error MalformedToken => Error.Validation(
        code: "ApiKey.MalformedToken",
        description: "No se pudo generar el token de la credencial.");

    public static Error LabelTooLong => Error.Validation(
        code: "ApiKey.LabelTooLong",
        description: $"La etiqueta de la credencial admite hasta {ApiKey.MaxLabelLength} caracteres.");

    public static Error ExpirationInThePast => Error.Validation(
        code: "ApiKey.ExpirationInThePast",
        description: "La fecha de vencimiento de la credencial debe ser futura.");

    public static Error NotFound(Guid id) => Error.NotFound(
        code: "ApiKey.NotFound",
        description: $"No existe una credencial con id '{id}' para este contribuyente.");

    public static Error AlreadyRevoked => Error.Conflict(
        code: "ApiKey.AlreadyRevoked",
        description: "La credencial ya estaba revocada.");
}
