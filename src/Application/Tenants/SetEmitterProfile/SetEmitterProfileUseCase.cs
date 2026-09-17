using ErrorOr;
using FluentValidation;
using NovaFE.Application.Common;
using NovaFE.Application.Common.Interfaces;
using NovaFE.Application.Tenants.Contracts;
using NovaFE.Application.Tenants.Interfaces;
using NovaFE.Domain.Common;
using NovaFE.Domain.Tenants;
using Microsoft.Extensions.Logging;

namespace NovaFE.Application.Tenants.SetEmitterProfile;

/// <summary>
/// Upsert del perfil fiscal del emisor del tenant actual
/// (<c>ICurrentTenant</c>). El operador lo fija con <c>CurrentTenant.Set</c>
/// antes de llamar (<c>TenantsController</c>); self-service ya lo trae
/// resuelto de la sesión/API key.
/// </summary>
public sealed class SetEmitterProfileUseCase(
    ILoggerFactory loggerFactory,
    IValidator<SetEmitterProfileCommand> validator,
    ICurrentTenant currentTenant,
    ITenantReadRepository tenants,
    IEmitterProfileRepository profiles)
    : CommandUseCase<SetEmitterProfileCommand, EmitterProfileDto>(loggerFactory, validator)
{
    protected override async Task<ErrorOr<EmitterProfileDto>> ExecuteCore(
        SetEmitterProfileCommand request,
        CancellationToken ct)
    {
        if (!currentTenant.HasValue)
            return Errors.Auth.TenantNotResolved;

        var tenantId = currentTenant.TenantId!.Value;

        if (await tenants.GetByIdAsync(tenantId, ct) is null)
            return TenantErrors.NotFound(tenantId);

        var environment = DgiiEnvironment.GetAll()
            .First(e => string.Equals(e.Name, request.DefaultEnvironment.Trim(), StringComparison.OrdinalIgnoreCase));

        var existing = await profiles.GetByTenantAsync(tenantId, ct);

        if (existing is null)
        {
            var created = EmitterProfile.Create(
                tenantId,
                request.Address,
                request.Municipality,
                request.Province,
                request.Phones,
                request.Email,
                request.EconomicActivity,
                environment);
            if (created.IsError)
                return created.Errors;

            await profiles.AddAsync(created.Value, ct);
            return ToDto(created.Value);
        }

        var updated = existing.Update(
            request.Address,
            request.Municipality,
            request.Province,
            request.Phones,
            request.Email,
            request.EconomicActivity,
            environment);
        if (updated.IsError)
            return updated.Errors;

        await profiles.UpdateAsync(existing, ct);
        return ToDto(existing);
    }

    private static EmitterProfileDto ToDto(EmitterProfile profile) => new(
        profile.Id,
        profile.TenantId,
        profile.Address,
        profile.Municipality,
        profile.Province,
        profile.Phones,
        profile.Email,
        profile.EconomicActivity,
        profile.DefaultEnvironment.Name,
        profile.CreatedAt,
        profile.UpdatedAt);
}
