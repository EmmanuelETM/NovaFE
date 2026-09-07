using ErrorOr;
using NovaFE.Domain.Common;
using NovaFE.Domain.Common.Entities;

namespace NovaFE.Domain.Webhooks;

/// <summary>
/// Un endpoint HTTP al que NovaFE entrega notificaciones (RF-12.7). Lo administra
/// el propio contribuyente (recurso <c>ITenantOwned</c>, con RLS). El
/// <see cref="Secret"/> se guarda <b>en claro</b> porque hace falta para firmar
/// cada entrega (HMAC-SHA256); se enseña una sola vez, al crearlo y al rotarlo.
/// </summary>
public sealed class WebhookEndpoint : Entity<Guid>, ITenantOwned, IAuditableEntity, ISoftDeletable
{
    public const int MaxUrlLength = 2048;
    public const int MaxDescriptionLength = 200;
    public const int MaxEvents = 20;

    // Required by EF Core.
    private WebhookEndpoint()
    {
    }

    private WebhookEndpoint(
        Guid id,
        Guid tenantId,
        string url,
        string secret,
        string? description,
        string[] events)
        : base(id)
    {
        TenantId = tenantId;
        Url = url;
        Secret = secret;
        Description = description;
        Events = events;
        Enabled = true;
    }

    public Guid TenantId { get; private set; }

    public string Url { get; private set; } = null!;

    /// <summary>Secret compartido para la firma HMAC. En claro (se necesita para firmar).</summary>
    public string Secret { get; private set; } = null!;

    public string? Description { get; private set; }

    /// <summary>Tipos suscritos: exactos (<c>ecf.accepted</c>) o comodines (<c>ecf.*</c>, <c>*</c>).</summary>
    public string[] Events { get; private set; } = [];

    /// <summary>Un endpoint deshabilitado no recibe entregas. El cliente lo re-habilita con un PATCH.</summary>
    public bool Enabled { get; private set; }

    /// <summary>Entregas fallidas seguidas. Se reinicia con una entrega exitosa o al re-habilitar.</summary>
    public int ConsecutiveFailures { get; private set; }

    public string? DisabledReason { get; private set; }

    public DateTimeOffset? DisabledAt { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }

    /// <summary>
    /// Crea un endpoint. El <paramref name="secret"/> lo genera el caso de uso
    /// (ver <c>WebhookSecret</c>). Valida forma de la URL, esquema http/https,
    /// largos y que cada evento sea suscribible. La regla de <c>https</c>
    /// obligatorio y el chequeo anti-SSRF (E/S) los hace el caso de uso.
    /// </summary>
    public static ErrorOr<WebhookEndpoint> Create(
        Guid tenantId,
        string? url,
        IEnumerable<string>? events,
        string? description,
        string secret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);

        if (tenantId == Guid.Empty)
            return WebhookEndpointErrors.TenantRequired;

        var normalizedUrl = NormalizeUrl(url);
        if (normalizedUrl.IsError)
            return normalizedUrl.Errors;

        var normalizedEvents = NormalizeEvents(events);
        if (normalizedEvents.IsError)
            return normalizedEvents.Errors;

        var normalizedDescription = NormalizeDescription(description);
        if (normalizedDescription.IsError)
            return normalizedDescription.Errors;

        return new WebhookEndpoint(
            Guid.CreateVersion7(),
            tenantId,
            normalizedUrl.Value,
            secret,
            normalizedDescription.Value,
            normalizedEvents.Value);
    }

    /// <summary>
    /// Actualización parcial: un argumento <c>null</c> deja el campo como está.
    /// Para borrar la descripción, pasar cadena vacía. El estado
    /// habilitado/deshabilitado se cambia con <see cref="SetEnabled"/>.
    /// </summary>
    public ErrorOr<Success> Update(string? url, IEnumerable<string>? events, string? description)
    {
        var changed = false;

        if (url is not null)
        {
            var normalized = NormalizeUrl(url);
            if (normalized.IsError)
                return normalized.Errors;
            Url = normalized.Value;
            changed = true;
        }

        if (events is not null)
        {
            var normalized = NormalizeEvents(events);
            if (normalized.IsError)
                return normalized.Errors;
            Events = normalized.Value;
            changed = true;
        }

        if (description is not null)
        {
            var normalized = NormalizeDescription(description);
            if (normalized.IsError)
                return normalized.Errors;
            Description = normalized.Value;
            changed = true;
        }

        return changed ? Result.Success : WebhookEndpointErrors.NothingToUpdate;
    }

    public void SetEnabled(bool enabled)
    {
        Enabled = enabled;

        if (enabled)
        {
            ConsecutiveFailures = 0;
            DisabledReason = null;
            DisabledAt = null;
        }
    }

    public void RotateSecret(string newSecret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newSecret);
        Secret = newSecret;
    }

    /// <summary>Una entrega llegó (2xx). Reinicia el contador de fallos.</summary>
    public void RecordDeliverySuccess() => ConsecutiveFailures = 0;

    /// <summary>
    /// Una entrega falló (o agotó los reintentos). Deshabilita el endpoint si se
    /// alcanzó <paramref name="autoDisableThreshold"/> fallos seguidos.
    /// </summary>
    public void RecordDeliveryFailure(int autoDisableThreshold, DateTimeOffset at)
    {
        ConsecutiveFailures++;

        if (Enabled && autoDisableThreshold > 0 && ConsecutiveFailures >= autoDisableThreshold)
        {
            Enabled = false;
            DisabledReason = $"{ConsecutiveFailures} entregas fallidas consecutivas.";
            DisabledAt = at;
        }
    }

    /// <summary>¿Este endpoint recibe <paramref name="eventType"/>?</summary>
    public bool IsSubscribedTo(string eventType) =>
        Events.Any(subscription => WebhookEventType.Covers(subscription, eventType));

    private static ErrorOr<string> NormalizeUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return WebhookEndpointErrors.UrlRequired;

        url = url.Trim();

        if (url.Length > MaxUrlLength)
            return WebhookEndpointErrors.UrlTooLong;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return WebhookEndpointErrors.UrlNotAbsolute;

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return WebhookEndpointErrors.UrlSchemeNotAllowed;

        return uri.ToString();
    }

    private static ErrorOr<string[]> NormalizeEvents(IEnumerable<string>? events)
    {
        if (events is null)
            return WebhookEndpointErrors.NoEvents;

        var cleaned = events
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Select(e => e.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (cleaned.Length == 0)
            return WebhookEndpointErrors.NoEvents;

        if (cleaned.Length > MaxEvents)
            return WebhookEndpointErrors.TooManyEvents;

        foreach (var value in cleaned)
        {
            if (!WebhookEventType.IsValidSubscription(value))
                return WebhookEndpointErrors.UnknownEventType(value);
        }

        // El comodín total absorbe al resto.
        if (cleaned.Contains("*"))
            return new[] { "*" };

        Array.Sort(cleaned, StringComparer.Ordinal);
        return cleaned;
    }

    private static ErrorOr<string?> NormalizeDescription(string? description)
    {
        if (description is null)
            return (string?)null;

        var trimmed = description.Trim();
        if (trimmed.Length == 0)
            return (string?)null;

        return trimmed.Length > MaxDescriptionLength
            ? WebhookEndpointErrors.DescriptionTooLong
            : trimmed;
    }
}
