using ErrorOr;
using FluentValidation;
using NovaFE.Application.Common;
using NovaFE.Application.Tenants.Contracts;
using NovaFE.Application.Tenants.Interfaces;
using NovaFE.Domain.Common;
using NovaFE.Domain.Tenants;
using Microsoft.Extensions.Logging;

namespace NovaFE.Application.Tenants.SetEmitterProfile;

/// <summary>
/// Upsert del perfil fiscal del emisor. Recurso de operador: no exige un tenant en
/// la petición, pero el contribuyente indicado debe existir.
/// </summary>
public sealed class SetEmitterProfileUseCase(
    ILoggerFactory loggerFactory,
    IValidator<SetEmitterProfileCommand> validator,
    ITenantReadRepository tenants,
    IEmitterProfileRepository profiles)
    : CommandUseCase<SetEmitterProfileCommand, EmitterProfileDto>(loggerFactory, validator)
{
    protected override async Task<ErrorOr<EmitterProfileDto>> ExecuteCore(
        SetEmitterProfileCommand request,
        CancellationToken ct)
    {
        if (await tenants.GetByIdAsync(request.TenantId, ct) is null)
            return TenantErrors.NotFound(request.TenantId);

        var environment = DgiiEnvironment.GetAll()
            .First(e => string.Equals(e.Name, request.DefaultEnvironment.Trim(), StringComparison.OrdinalIgnoreCase));

        var existing = await profiles.GetByTenantAsync(request.TenantId, ct);

        if (existing is null)
        {
            var created = EmitterProfile.Create(
                request.TenantId,
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
