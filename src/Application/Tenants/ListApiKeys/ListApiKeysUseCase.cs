using ErrorOr;
using NovaFE.Application.Common;
using NovaFE.Application.Tenants.Contracts;
using NovaFE.Application.Tenants.Interfaces;
using NovaFE.Domain.Tenants;
using Microsoft.Extensions.Logging;

namespace NovaFE.Application.Tenants.ListApiKeys;

/// <summary>Lista las API keys de un contribuyente (sin tokens). Recurso de operador.</summary>
public sealed class ListApiKeysUseCase(
    ILoggerFactory loggerFactory,
    ITenantReadRepository tenants,
    IApiKeyReadRepository apiKeys)
    : QueryUseCase<ListApiKeysQuery, IReadOnlyList<ApiKeyDto>>(loggerFactory)
{
    protected override async Task<ErrorOr<IReadOnlyList<ApiKeyDto>>> ExecuteCore(
        ListApiKeysQuery request,
        CancellationToken ct)
    {
        if (await tenants.GetByIdAsync(request.TenantId, ct) is null)
            return TenantErrors.NotFound(request.TenantId);

        var list = await apiKeys.ListByTenantAsync(request.TenantId, ct);
        return ErrorOrFactory.From(list);
    }
}
