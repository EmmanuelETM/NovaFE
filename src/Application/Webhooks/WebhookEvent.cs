using NovaFE.Application.Webhooks.Contracts;

namespace NovaFE.Application.Webhooks;

/// <summary>Arma el sobre de un evento de webhook.</summary>
public static class WebhookEvent
{
    public const string ApiVersion = "1";
    private const string ObjectName = "event";
    private const string IdPrefix = "evt_";

    /// <summary>Id único de evento (UUIDv7 ordenable en el tiempo, con prefijo <c>evt_</c>).</summary>
    public static string NewId() => IdPrefix + Guid.CreateVersion7().ToString("N");

    public static WebhookEventEnvelope Create(string type, object dataObject, DateTimeOffset createdAt) =>
        new(NewId(), ObjectName, type, ApiVersion, createdAt, new WebhookEventData(dataObject));
}
