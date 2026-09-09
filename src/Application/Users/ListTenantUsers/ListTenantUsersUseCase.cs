using ErrorOr;
using Microsoft.Extensions.Logging;
using NovaFE.Application.Common;
using NovaFE.Application.Tenants.Interfaces;
using NovaFE.Application.Users.Contracts;
using NovaFE.Application.Users.Interfaces;
using NovaFE.Domain.Common;
using NovaFE.Domain.Tenants;

namespace NovaFE.Application.Users.ListTenantUsers;

/// <summary>Lista los usuarios de un contribuyente. Recurso de operador.</summary>
public sealed class ListTenantUsersUseCase(
    ILoggerFactory loggerFactory,
    ITenantReadRepository tenants,
    IPlatformUserReadRepository users)
    : QueryUseCase<ListTenantUsersQuery, IReadOnlyList<PlatformUserDto>>(loggerFactory)
{
    protected override async Task<ErrorOr<IReadOnlyList<PlatformUserDto>>> ExecuteCore(
        ListTenantUsersQuery request,
        CancellationToken ct)
    {
        if (await tenants.GetByIdAsync(request.TenantId, ct) is null)
            return TenantErrors.NotFound(request.TenantId);

        return ErrorOrFactory.From(await users.ListByTenantAsync(request.TenantId, ct));
    }
}
