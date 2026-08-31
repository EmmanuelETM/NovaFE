using ErrorOr;
using NovaFE.Application.Common;
using NovaFE.Application.Common.Interfaces;
using NovaFE.Application.Ecf.Contracts;
using NovaFE.Application.Ecf.Interfaces;
using NovaFE.Domain.Common;
using NovaFE.Domain.Ecf;
using Microsoft.Extensions.Logging;

namespace NovaFE.Application.Ecf.GetEcf;

public sealed class GetEcfUseCase(
    ILoggerFactory loggerFactory,
    ICurrentTenant currentTenant,
    IEcfReadRepository ecf)
    : QueryUseCase<GetEcfQuery, EcfDto>(loggerFactory)
{
    protected override async Task<ErrorOr<EcfDto>> ExecuteCore(GetEcfQuery request, CancellationToken ct)
    {
        if (currentTenant.TenantId is not { } tenantId)
            return Errors.Auth.TenantNotResolved;

        var dto = await ecf.GetByIdAsync(request.Id, tenantId, ct);
        return dto is null ? EcfErrors.NotFound(request.Id) : dto;
    }
}

public sealed class GetEcfXmlUseCase(
    ILoggerFactory loggerFactory,
    ICurrentTenant currentTenant,
    IEcfReadRepository ecf)
    : QueryUseCase<GetEcfXmlQuery, string>(loggerFactory)
{
    protected override async Task<ErrorOr<string>> ExecuteCore(GetEcfXmlQuery request, CancellationToken ct)
    {
        if (currentTenant.TenantId is not { } tenantId)
            return Errors.Auth.TenantNotResolved;

        var xml = await ecf.GetXmlAsync(request.Id, tenantId, request.Rfce, ct);
        return xml is null ? EcfErrors.NotFound(request.Id) : xml;
    }
}
