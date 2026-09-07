namespace NovaFE.Application.Webhooks.Interfaces;

/// <summary>Hace el <c>POST</c> firmado al endpoint del cliente. Sin reintento propio — de eso se encarga el outbox.</summary>
public interface IWebhookSender
{
    Task<WebhookDeliveryAttempt> SendAsync(WebhookDeliveryRequest request, CancellationToken ct = default);
}

/// <param name="Url">Destino.</param>
/// <param name="Secret">Secret del endpoint, para la firma.</param>
/// <param name="EventType">Va en <c>X-NovaFE-Event</c>.</param>
/// <param name="DeliveryId">Va en <c>X-NovaFE-Delivery</c>; estable entre reintentos de la misma fila.</param>
/// <param name="Body">El JSON del sobre, tal cual se entrega y se firma.</param>
public sealed record WebhookDeliveryRequest(
    string Url,
    string Secret,
    string EventType,
    string DeliveryId,
    string Body);

/// <param name="Success">La respuesta fue <c>2xx</c>.</param>
/// <param name="StatusCode">Código HTTP, o <c>null</c> si no hubo respuesta (timeout / conexión).</param>
/// <param name="Error">Detalle del fallo, o <c>null</c> si fue exitoso.</param>
public sealed record WebhookDeliveryAttempt(bool Success, int? StatusCode, string? Error);
