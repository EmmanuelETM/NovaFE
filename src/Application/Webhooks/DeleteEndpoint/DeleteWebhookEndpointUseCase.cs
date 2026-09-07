using ErrorOr;
using NovaFE.Application.Common;
using NovaFE.Application.Common.Interfaces;
using NovaFE.Application.Webhooks.Interfaces;
using NovaFE.Domain.Common;
using NovaFE.Domain.Webhooks;
using Microsoft.Extensions.Logging;

namespace NovaFE.Application.Webhooks.DeleteEndpoint;

public sealed class DeleteWebhookEndpointUseCase(
    ILoggerFactory loggerFactory,
    ICurrentTenant currentTenant,
    IWebhookEndpointRepository endpoints)
    : CommandUseCase<DeleteWebhookEndpointCommand>(loggerFactory)
{
    protected override async Task<ErrorOr<Success>> ExecuteCore(
        DeleteWebhookEndpointCommand request,
        CancellationToken ct)
    {
        if (!currentTenant.HasValue)
            return Errors.Auth.TenantNotResolved;

        var endpoint = await endpoints.GetAsync(request.Id, ct);
        if (endpoint is null)
            return WebhookEndpointErrors.NotFound(request.Id);

        await endpoints.RemoveAsync(endpoint, ct);

        return Result.Success;
    }
}
