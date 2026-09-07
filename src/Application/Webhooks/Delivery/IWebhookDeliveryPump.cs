namespace NovaFE.Application.Webhooks.Delivery;

/// <summary>
/// Un tick del worker de entrega: recupera filas atascadas, reclama un lote y las
/// entrega (cada una en su propio scope con el tenant fijado). Seam para que las
/// pruebas lo disparen de forma determinista.
/// </summary>
public interface IWebhookDeliveryPump
{
    Task<int> RunOnceAsync(CancellationToken ct = default);
}
