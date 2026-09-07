using ErrorOr;
using NovaFE.Application.Common;
using NovaFE.Application.Common.Interfaces;
using NovaFE.Application.Webhooks.Contracts;
using NovaFE.Application.Webhooks.Interfaces;
using NovaFE.Domain.Common;
using NovaFE.Domain.Webhooks;
using Microsoft.Extensions.Logging;

namespace NovaFE.Application.Webhooks.GetEndpoint;

public sealed class GetWebhookEndpointUseCase(
    ILoggerFactory loggerFactory,
    ICurrentTenant currentTenant,
    IWebhookEndpointReadRepository endpoints)
    : QueryUseCase<GetWebhookEndpointQuery, WebhookEndpointDto>(loggerFactory)
{
    protected override async Task<ErrorOr<WebhookEndpointDto>> ExecuteCore(
        GetWebhookEndpointQuery request,
        CancellationToken ct)
    {
        if (currentTenant.TenantId is not { } tenantId)
            return Errors.Auth.TenantNotResolved;

        var endpoint = await endpoints.GetAsync(request.Id, tenantId, ct);

        return endpoint is null
            ? WebhookEndpointErrors.NotFound(request.Id)
            : endpoint;
    }
}
