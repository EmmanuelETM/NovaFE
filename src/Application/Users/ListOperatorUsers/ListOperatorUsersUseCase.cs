using ErrorOr;
using Microsoft.Extensions.Logging;
using NovaFE.Application.Common;
using NovaFE.Application.Users.Contracts;
using NovaFE.Application.Users.Interfaces;
using NovaFE.Domain.Common;

namespace NovaFE.Application.Users.ListOperatorUsers;

/// <summary>Lista los operadores del SaaS (<c>tenant_id</c> nulo). Recurso de operador.</summary>
public sealed class ListOperatorUsersUseCase(
    ILoggerFactory loggerFactory,
    IPlatformUserReadRepository users)
    : ParameterlessQueryUseCase<IReadOnlyList<PlatformUserDto>>(loggerFactory)
{
    protected override async Task<ErrorOr<IReadOnlyList<PlatformUserDto>>> ExecuteCore(
        NoRequest request,
        CancellationToken ct)
        => ErrorOrFactory.From(await users.ListOperatorsAsync(ct));
}
