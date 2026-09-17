using ErrorOr;
using FluentValidation;
using NovaFE.Application.Common;
using NovaFE.Application.Common.Interfaces;
using NovaFE.Application.Tenants.Contracts;
using NovaFE.Application.Tenants.Interfaces;
using NovaFE.Domain.Common;
using NovaFE.Domain.Tenants;
using Microsoft.Extensions.Logging;

namespace NovaFE.Application.Tenants.UpdateEmitterProfile;

/// <summary>
/// Self-service: actualiza el perfil del tenant actual sin tocar su
/// <c>DefaultEnvironment</c> (se conserva el que ya tenía) y sin crearlo si
/// no existe todavía — ver el comentario de <see cref="UpdateEmitterProfileCommand"/>.
/// </summary>
public sealed class UpdateEmitterProfileUseCase(
    ILoggerFactory loggerFactory,
    IValidator<UpdateEmitterProfileCommand> validator,
    ICurrentTenant currentTenant,
    IEmitterProfileRepository profiles)
    : CommandUseCase<UpdateEmitterProfileCommand, EmitterProfileDto>(loggerFactory, validator)
{
    protected override async Task<ErrorOr<EmitterProfileDto>> ExecuteCore(
        UpdateEmitterProfileCommand request,
        CancellationToken ct)
    {
        if (!currentTenant.HasValue)
            return Errors.Auth.TenantNotResolved;

        var existing = await profiles.GetByTenantAsync(currentTenant.TenantId!.Value, ct);
        if (existing is null)
            return EmitterProfileErrors.NotConfigured;

        var updated = existing.Update(
            request.Address,
            request.Municipality,
            request.Province,
            request.Phones,
            request.Email,
            request.EconomicActivity,
            existing.DefaultEnvironment);
        if (updated.IsError)
            return updated.Errors;

        await profiles.UpdateAsync(existing, ct);

        return new EmitterProfileDto(
            existing.Id,
            existing.TenantId,
            existing.Address,
            existing.Municipality,
            existing.Province,
            existing.Phones,
            existing.Email,
            existing.EconomicActivity,
            existing.DefaultEnvironment.Name,
            existing.CreatedAt,
            existing.UpdatedAt);
    }
}
