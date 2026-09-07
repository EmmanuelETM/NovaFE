using ErrorOr;
using NovaFE.Application.Common;
using NovaFE.Application.Common.Interfaces;
using NovaFE.Application.Webhooks.Contracts;
using NovaFE.Application.Webhooks.Interfaces;
using NovaFE.Domain.Common;
using NovaFE.Domain.Webhooks;
using Microsoft.Extensions.Logging;

namespace NovaFE.Application.Webhooks.PingEndpoint;

/// <summary>
/// Entrega un <c>webhook.ping</c> al endpoint <b>de forma inline</b> (no pasa por
/// el outbox) y devuelve el resultado, para que el cliente pruebe su receptor.
/// Se entrega aunque el endpoint no esté suscrito a nada.
/// </summary>
public sealed class PingWebhookEndpointUseCase(
    ILoggerFactory loggerFactory,
    ICurrentTenant currentTenant,
    TimeProvider timeProvider,
    IWebhookEndpointRepository endpoints,
    IWebhookSender sender)
    : CommandUseCase<PingWebhookEndpointCommand, WebhookPingResultDto>(loggerFactory)
{
    protected override async Task<ErrorOr<WebhookPingResultDto>> ExecuteCore(
        PingWebhookEndpointCommand request,
        CancellationToken ct)
    {
        if (!currentTenant.HasValue)
            return Errors.Auth.TenantNotResolved;

        var endpoint = await endpoints.GetAsync(request.Id, ct);
        if (endpoint is null)
            return WebhookEndpointErrors.NotFound(request.Id);

        var envelope = WebhookEvent.Create(
            WebhookEventType.Ping,
            new { message = "ping", endpointId = endpoint.Id },
            timeProvider.GetUtcNow());

        var body = System.Text.Json.JsonSerializer.Serialize(envelope, NovaFE.Domain.Common.Json.JsonSettings.Bulletproof);

        var attempt = await sender.SendAsync(
            new WebhookDeliveryRequest(
                endpoint.Url, endpoint.Secret, WebhookEventType.Ping, envelope.Id, body),
            ct);

        return new WebhookPingResultDto(attempt.Success, attempt.StatusCode, attempt.Error);
    }
}
