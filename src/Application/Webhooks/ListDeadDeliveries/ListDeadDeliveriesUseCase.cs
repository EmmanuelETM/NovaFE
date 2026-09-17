using ErrorOr;
using NovaFE.Application.Common;
using NovaFE.Application.Webhooks.Contracts;
using NovaFE.Application.Webhooks.Interfaces;
using NovaFE.Domain.Common;
using Microsoft.Extensions.Logging;

namespace NovaFE.Application.Webhooks.ListDeadDeliveries;

public sealed class ListDeadDeliveriesUseCase(
    ILoggerFactory loggerFactory,
    IWebhookDeliveryReadRepository deliveries)
    : QueryUseCase<ListDeadDeliveriesQuery, PagedResult<DeadWebhookDeliveryDto>>(loggerFactory)
{
    protected override async Task<ErrorOr<PagedResult<DeadWebhookDeliveryDto>>> ExecuteCore(
        ListDeadDeliveriesQuery request,
        CancellationToken ct)
        => await deliveries.ListDeadAcrossTenantsAsync(request.Page, request.PageSize, ct);
}
