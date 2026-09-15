namespace NovaFE.Domain.Webhooks;

/// <summary>
/// Catálogo de tipos de evento de webhook. Formato <c>categoria.evento</c>. Cubre
/// el ciclo de vida del e-CF, los avisos de vencimiento de certificados y
/// secuencias (RF-01.6) y la contingencia M11 Tipo 1 (RF-... Decreto 587-24);
/// los de M5 / M8 / M11 Tipo 2-3 llegan con su módulo (ver <c>docs/webhooks.md</c>).
/// </summary>
public static class WebhookEventType
{
    public const string EcfSubmitted = "ecf.submitted";
    public const string EcfAccepted = "ecf.accepted";
    public const string EcfAcceptedConditional = "ecf.accepted_conditional";
    public const string EcfRejected = "ecf.rejected";
    public const string EcfReview = "ecf.review";
    public const string EcfFailed = "ecf.failed";

    /// <summary>
    /// Detección por huella (RF-12.7 + detección de duplicados): modo
    /// <c>observar</c> — el e-CF se emitió igual, pero se parece a uno reciente
    /// del mismo comprador (RNC/cédula, obligatorio para esta detección).
    /// </summary>
    public const string EcfDuplicateSuspected = "ecf.duplicate_suspected";

    public const string CertificateExpiring = "certificate.expiring";
    public const string CertificateExpired = "certificate.expired";

    public const string SequenceLow = "sequence.low";
    public const string SequenceExhausted = "sequence.exhausted";
    public const string SequenceExpiring = "sequence.expiring";
    public const string SequenceExpired = "sequence.expired";

    /// <summary>
    /// Contingencia M11 Tipo 1 (falta de conectividad): <c>platform.contingency_mode</c>
    /// se prendió, sola o a mano — no es un evento por tenant, se entrega a todo
    /// suscriptor activo.
    /// </summary>
    public const string ContingencyActivated = "contingency.activated";

    /// <summary>Igual que <see cref="ContingencyActivated"/> pero al apagarse.</summary>
    public const string ContingencyDeactivated = "contingency.deactivated";

    /// <summary>
    /// Evento sintético de prueba (<c>POST /webhooks/{id}/ping</c>). Se entrega al
    /// endpoint indicado sin importar sus suscripciones — no es un tipo suscribible.
    /// </summary>
    public const string Ping = "webhook.ping";

    /// <summary>Los tipos a los que un endpoint puede suscribirse (exactos).</summary>
    public static readonly IReadOnlyList<string> Subscribable =
    [
        EcfSubmitted,
        EcfAccepted,
        EcfAcceptedConditional,
        EcfRejected,
        EcfReview,
        EcfFailed,
        EcfDuplicateSuspected,
        CertificateExpiring,
        CertificateExpired,
        SequenceLow,
        SequenceExhausted,
        SequenceExpiring,
        SequenceExpired,
        ContingencyActivated,
        ContingencyDeactivated,
    ];

    private static readonly IReadOnlyList<string> Categories = ["ecf", "certificate", "sequence", "contingency"];

    /// <summary>
    /// ¿<paramref name="value"/> es una entrada válida en la lista <c>events</c> de
    /// una suscripción? Acepta un tipo exacto, un comodín de categoría
    /// (<c>ecf.*</c>) o el comodín total (<c>*</c>).
    /// </summary>
    public static bool IsValidSubscription(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        value = value.Trim();

        if (value == "*")
            return true;

        if (value.EndsWith(".*", StringComparison.Ordinal))
            return Categories.Contains(value[..^2], StringComparer.Ordinal);

        return Subscribable.Contains(value, StringComparer.Ordinal);
    }

    /// <summary>¿La suscripción <paramref name="subscription"/> cubre <paramref name="eventType"/>?</summary>
    public static bool Covers(string subscription, string eventType)
    {
        ArgumentNullException.ThrowIfNull(subscription);
        ArgumentNullException.ThrowIfNull(eventType);

        if (subscription == "*")
            return true;

        if (subscription.EndsWith(".*", StringComparison.Ordinal))
            return eventType.StartsWith(subscription[..^1], StringComparison.Ordinal);

        return string.Equals(subscription, eventType, StringComparison.Ordinal);
    }
}
