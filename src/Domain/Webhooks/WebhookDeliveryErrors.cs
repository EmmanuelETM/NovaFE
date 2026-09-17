using ErrorOr;

namespace NovaFE.Domain.Webhooks;

/// <summary>
/// Errores de negocio de las entregas de webhook (<c>webhook_deliveries</c>).
/// Entidad distinta de <see cref="WebhookEndpoint"/> — de ahí el error-file
/// propio, mismo criterio que <see cref="WebhookEndpointErrors"/>.
/// </summary>
public static class WebhookDeliveryErrors
{
    /// <summary>No existe, no es de este contribuyente, o no está en estado <c>dead</c>.</summary>
    public static Error NotFound(Guid id) => Error.NotFound(
        code: "WebhookDelivery.NotFound",
        description: $"No existe una entrega muerta con id '{id}' para este contribuyente.");
}
