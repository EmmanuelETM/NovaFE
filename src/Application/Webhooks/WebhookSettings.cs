namespace NovaFE.Application.Webhooks;

/// <summary>
/// Parámetros de webhooks que necesitan las capas internas. La capa Service los
/// llena desde configuración (<c>WebhooksOptions</c>); los defaults sirven para
/// pruebas y arranque. Los de entrega (timeout, backoff, retención) se agregan
/// con el slice de entrega.
/// </summary>
public sealed record WebhookSettings
{
    /// <summary>Tope de endpoints por contribuyente.</summary>
    public int MaxEndpointsPerTenant { get; init; } = 5;

    /// <summary>Exige <c>https</c> en la URL de destino. <c>false</c> en Development (listeners locales).</summary>
    public bool RequireHttps { get; init; } = true;
}
