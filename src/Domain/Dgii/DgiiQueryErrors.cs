using ErrorOr;
using NovaFE.Domain.Common;

namespace NovaFE.Domain.Dgii;

/// <summary>
/// Errores de las consultas a la DGII (Módulo 10): trackIds, directorio y estatus
/// de servicio. Los fallos de red/timeout se mapean con <c>HttpErrorMapper</c> a
/// <c>Errors.Http.*</c>; estos son los específicos de estos servicios.
/// </summary>
public static class DgiiQueryErrors
{
    /// <summary>
    /// CerteCF no lista <c>consultatrackids</c> ni <c>consultadirectorio</c> como
    /// ambiente disponible (§5.10 de la Descripción Técnica de Servicios DGII) —
    /// solo TesteCF y Producción. El cliente devuelve esto sin intentar la llamada.
    /// </summary>
    public static Error NotAvailableInEnvironment(string service, DgiiEnvironment environment) => Error.Failure(
        code: "Dgii.Query.NotAvailableInEnvironment",
        description: $"{service} no está disponible en el ambiente {environment.DisplayName}.");

    public static Error QueryFailed(string service, int statusCode) => Error.Failure(
        code: "Dgii.Query.QueryFailed",
        description: $"La consulta {service} a la DGII falló (HTTP {statusCode}).");

    public static Error MalformedResponse => Error.Failure(
        code: "Dgii.Query.MalformedResponse",
        description: "La respuesta de la DGII no tiene el formato esperado.");

    /// <summary>
    /// El estatus de servicio (<c>statusecf.dgii.gov.do</c>) usa una API key propia
    /// (<c>Dgii:StatusApiKey</c>), distinta del token Bearer del resto de servicios.
    /// La DGII la entrega al iniciar la integración técnica — sin ella, ni se
    /// intenta la llamada.
    /// </summary>
    public static Error StatusApiKeyNotConfigured => Error.Failure(
        code: "Dgii.Query.StatusApiKeyNotConfigured",
        description: "Dgii:StatusApiKey no está configurada.");
}
