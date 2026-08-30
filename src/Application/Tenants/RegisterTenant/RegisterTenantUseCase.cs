using ErrorOr;
using FluentValidation;
using NovaFE.Application.Common;
using NovaFE.Application.Tenants.Interfaces;
using NovaFE.Domain.Common;
using NovaFE.Domain.Tenants;
using Microsoft.Extensions.Logging;

namespace NovaFE.Application.Tenants.RegisterTenant;

public sealed class RegisterTenantUseCase(
    ILoggerFactory loggerFactory,
    IValidator<RegisterTenantCommand> validator,
    ITenantRepository tenants)
    : CommandUseCase<RegisterTenantCommand, Guid>(loggerFactory, validator)
{
    protected override async Task<ErrorOr<Guid>> ExecuteCore(
        RegisterTenantCommand request,
        CancellationToken ct)
    {
        var rncResult = Rnc.Create(request.Rnc);
        if (rncResult.IsError)
            return rncResult.Errors;

        var rnc = rncResult.Value;

        var plan = TenantPlan.GetAll()
            .FirstOrDefault(p => string.Equals(p.Name, request.Plan.Trim(), StringComparison.OrdinalIgnoreCase));
        if (plan is null)
            return TenantErrors.UnknownPlan(request.Plan);

        if (await tenants.RncExistsAsync(rnc.Value, ct))
            return TenantErrors.RncAlreadyRegistered(rnc.Value);

        var tenant = Tenant.Register(rnc, request.LegalName, request.TradeName, plan);

        await tenants.AddAsync(tenant, ct);

        return tenant.Id;
    }
}
