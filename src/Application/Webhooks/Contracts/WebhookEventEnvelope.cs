using System.Diagnostics.CodeAnalysis;

namespace NovaFE.Application.Webhooks.Contracts;

/// <summary>
/// El cuerpo de una entrega de webhook (convención Stripe/GitHub). Se serializa
/// una sola vez al encolar y se entrega verbatim — la firma es sobre ese JSON.
/// Ver <c>docs/webhooks.md</c>.
/// </summary>
[SuppressMessage("Naming", "CA1720:Identifier contains type name",
    Justification = "'object' es el nombre literal del campo en la convención de sobre de Stripe/GitHub.")]
public sealed record WebhookEventEnvelope(
    string Id,
    string Object,
    string Type,
    string ApiVersion,
    DateTimeOffset CreatedAt,
    WebhookEventData Data);

/// <summary>El recurso al que se refiere el evento, con el mismo shape que su <c>GET</c>.</summary>
[SuppressMessage("Naming", "CA1720:Identifier contains type name",
    Justification = "'object' es el nombre literal del campo en la convención de sobre de Stripe/GitHub.")]
public sealed record WebhookEventData(object Object);
