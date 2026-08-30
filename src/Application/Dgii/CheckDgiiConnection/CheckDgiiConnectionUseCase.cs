using ErrorOr;
using FluentValidation;
using NovaFE.Application.Common;
using NovaFE.Application.Common.Interfaces;
using NovaFE.Application.Dgii.Contracts;
using NovaFE.Application.Dgii.Interfaces;
using NovaFE.Domain.Common;
using Microsoft.Extensions.Logging;

namespace NovaFE.Application.Dgii.CheckDgiiConnection;

public sealed class CheckDgiiConnectionUseCase(
    ILoggerFactory loggerFactory,
    IValidator<CheckDgiiConnectionQuery> validator,
    ICurrentTenant currentTenant,
    IDgiiTokenProvider tokenProvider)
    : QueryUseCase<CheckDgiiConnectionQuery, DgiiConnectionDto>(loggerFactory, validator)
{
    protected override async Task<ErrorOr<DgiiConnectionDto>> ExecuteCore(
        CheckDgiiConnectionQuery request,
        CancellationToken ct)
    {
        if (!currentTenant.HasValue)
            return Errors.Auth.TenantNotResolved;

        var environment = DgiiEnvironment.GetAll()
            .First(e => string.Equals(e.Name, request.Environment.Trim(), StringComparison.OrdinalIgnoreCase));

        var token = await tokenProvider.GetTokenAsync(environment, ct);
        if (token.IsError)
            return token.Errors;

        return new DgiiConnectionDto(
            Connected: true,
            Environment: environment.Name,
            IssuedAt: token.Value.IssuedAt,
            ExpiresAt: token.Value.ExpiresAt);
    }
}
