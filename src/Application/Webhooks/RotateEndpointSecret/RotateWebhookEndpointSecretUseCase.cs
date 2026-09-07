using ErrorOr;
using NovaFE.Application.Common;
using NovaFE.Application.Common.Interfaces;
using NovaFE.Application.Webhooks.Contracts;
using NovaFE.Application.Webhooks.Interfaces;
using NovaFE.Domain.Common;
using NovaFE.Domain.Webhooks;
using Microsoft.Extensions.Logging;

namespace NovaFE.Application.Webhooks.RotateEndpointSecret;

public sealed class RotateWebhookEndpointSecretUseCase(
    ILoggerFactory loggerFactory,
    ICurrentTenant currentTenant,
    IWebhookEndpointRepository endpoints)
    : CommandUseCase<RotateWebhookEndpointSecretCommand, WebhookSecretDto>(loggerFactory)
{
    protected override async Task<ErrorOr<WebhookSecretDto>> ExecuteCore(
        RotateWebhookEndpointSecretCommand request,
        CancellationToken ct)
    {
        if (!currentTenant.HasValue)
            return Errors.Auth.TenantNotResolved;

        var endpoint = await endpoints.GetAsync(request.Id, ct);
        if (endpoint is null)
            return WebhookEndpointErrors.NotFound(request.Id);

        var secret = WebhookSecret.Generate();
        endpoint.RotateSecret(secret);

        await endpoints.UpdateAsync(endpoint, ct);

        return new WebhookSecretDto(secret);
    }
}
