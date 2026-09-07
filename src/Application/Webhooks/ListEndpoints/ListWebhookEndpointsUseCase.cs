using ErrorOr;
using NovaFE.Application.Common;
using NovaFE.Application.Common.Interfaces;
using NovaFE.Application.Webhooks.Contracts;
using NovaFE.Application.Webhooks.Interfaces;
using NovaFE.Domain.Common;
using Microsoft.Extensions.Logging;

namespace NovaFE.Application.Webhooks.ListEndpoints;

/// <summary>Lista los endpoints de webhook del contribuyente de la petición (sin secret).</summary>
public sealed class ListWebhookEndpointsUseCase(
    ILoggerFactory loggerFactory,
    ICurrentTenant currentTenant,
    IWebhookEndpointReadRepository endpoints)
    : ParameterlessQueryUseCase<IReadOnlyList<WebhookEndpointDto>>(loggerFactory)
{
    protected override async Task<ErrorOr<IReadOnlyList<WebhookEndpointDto>>> ExecuteCore(
        NoRequest request,
        CancellationToken ct)
    {
        if (currentTenant.TenantId is not { } tenantId)
            return Errors.Auth.TenantNotResolved;

        var list = await endpoints.ListByTenantAsync(tenantId, ct);
        return ErrorOrFactory.From(list);
    }
}
