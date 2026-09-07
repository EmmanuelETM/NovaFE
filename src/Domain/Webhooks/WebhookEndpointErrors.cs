using ErrorOr;

namespace NovaFE.Domain.Webhooks;

/// <summary>
/// Errores de negocio de los endpoints de webhook. <c>code</c> en inglés
/// (estable); la descripción la consume quien llama a la API, por eso va en español.
/// </summary>
public static class WebhookEndpointErrors
{
    public static Error TenantRequired => Error.Validation(
        code: "WebhookEndpoint.TenantRequired",
        description: "El webhook debe pertenecer a un contribuyente.");

    public static Error UrlRequired => Error.Validation(
        code: "WebhookEndpoint.UrlRequired",
        description: "La URL de destino es obligatoria.");

    public static Error UrlNotAbsolute => Error.Validation(
        code: "WebhookEndpoint.UrlNotAbsolute",
        description: "La URL de destino debe ser absoluta.");

    public static Error UrlTooLong => Error.Validation(
        code: "WebhookEndpoint.UrlTooLong",
        description: $"La URL de destino admite hasta {WebhookEndpoint.MaxUrlLength} caracteres.");

    public static Error UrlSchemeNotAllowed => Error.Validation(
        code: "WebhookEndpoint.UrlSchemeNotAllowed",
        description: "La URL de destino debe usar http o https.");

    public static Error HttpsRequired => Error.Validation(
        code: "WebhookEndpoint.HttpsRequired",
        description: "La URL de destino debe usar https.");

    public static Error DescriptionTooLong => Error.Validation(
        code: "WebhookEndpoint.DescriptionTooLong",
        description: $"La descripción admite hasta {WebhookEndpoint.MaxDescriptionLength} caracteres.");

    public static Error NoEvents => Error.Validation(
        code: "WebhookEndpoint.NoEvents",
        description: "Hay que suscribirse al menos a un evento.");

    public static Error TooManyEvents => Error.Validation(
        code: "WebhookEndpoint.TooManyEvents",
        description: $"Un webhook admite hasta {WebhookEndpoint.MaxEvents} suscripciones.");

    public static Error UnknownEventType(string value) => Error.Validation(
        code: "WebhookEndpoint.UnknownEventType",
        description: $"'{value}' no es un evento válido. Usá un tipo exacto (p. ej. 'ecf.accepted'), 'ecf.*' o '*'.");

    public static Error NothingToUpdate => Error.Validation(
        code: "WebhookEndpoint.NothingToUpdate",
        description: "No se indicó ningún campo a modificar.");

    public static Error NotFound(Guid id) => Error.NotFound(
        code: "WebhookEndpoint.NotFound",
        description: $"No existe un webhook con id '{id}' para este contribuyente.");

    public static Error LimitReached(int max) => Error.Conflict(
        code: "WebhookEndpoint.LimitReached",
        description: $"Se alcanzó el máximo de {max} webhooks para este contribuyente.");

    public static Error PrivateAddressNotAllowed => Error.Validation(
        code: "WebhookEndpoint.PrivateAddressNotAllowed",
        description: "La URL de destino resuelve a una dirección privada, de loopback o link-local, que no está permitida.");

    public static Error UnresolvableHost => Error.Validation(
        code: "WebhookEndpoint.UnresolvableHost",
        description: "No se pudo resolver el host de la URL de destino.");
}
