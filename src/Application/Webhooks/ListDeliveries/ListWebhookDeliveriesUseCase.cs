using ErrorOr;
using NovaFE.Application.Common;
using NovaFE.Application.Common.Interfaces;
using NovaFE.Application.Webhooks.Contracts;
using NovaFE.Application.Webhooks.Interfaces;
using NovaFE.Domain.Common;
using NovaFE.Domain.Webhooks;
using Microsoft.Extensions.Logging;

namespace NovaFE.Application.Webhooks.ListDeliveries;

public sealed class ListWebhookDeliveriesUseCase(
    ILoggerFactory loggerFactory,
    ICurrentTenant currentTenant,
    IWebhookEndpointReadRepository endpoints,
    IWebhookDeliveryReadRepository deliveries)
    : QueryUseCase<ListWebhookDeliveriesQuery, PagedResult<WebhookDeliveryDto>>(loggerFactory)
{
    protected override async Task<ErrorOr<PagedResult<WebhookDeliveryDto>>> ExecuteCore(
        ListWebhookDeliveriesQuery request,
        CancellationToken ct)
    {
        if (currentTenant.TenantId is not { } tenantId)
            return Errors.Auth.TenantNotResolved;

        if (await endpoints.GetAsync(request.EndpointId, tenantId, ct) is null)
            return WebhookEndpointErrors.NotFound(request.EndpointId);

        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var result = await deliveries.ListByEndpointAsync(request.EndpointId, tenantId, page, pageSize, ct);
        return result;
    }
}
