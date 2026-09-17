using ErrorOr;
using NovaFE.Application.Common;
using NovaFE.Application.Common.Interfaces;
using NovaFE.Application.Tenants.Contracts;
using NovaFE.Application.Tenants.Interfaces;
using NovaFE.Domain.Common;
using NovaFE.Domain.Tenants;
using Microsoft.Extensions.Logging;

namespace NovaFE.Application.Tenants.GetEmitterProfile;

/// <summary>
/// El perfil fiscal del emisor del tenant actual. Operador y self-service
/// comparten este caso de uso: <c>TenantsController</c> fija
/// <c>ICurrentTenant</c> con <c>CurrentTenant.Set</c> antes de llamarlo (ver su
/// comentario); <c>EmitterProfileController</c> ya lo trae resuelto de la
/// sesión/API key.
/// </summary>
public sealed class GetEmitterProfileUseCase(
    ILoggerFactory loggerFactory,
    ICurrentTenant currentTenant,
    IEmitterProfileReadRepository profiles)
    : ParameterlessQueryUseCase<EmitterProfileDto>(loggerFactory)
{
    protected override async Task<ErrorOr<EmitterProfileDto>> ExecuteCore(
        NoRequest request,
        CancellationToken ct)
    {
        if (!currentTenant.HasValue)
            return Errors.Auth.TenantNotResolved;

        var profile = await profiles.GetByTenantAsync(currentTenant.TenantId!.Value, ct);

        return profile is null
            ? EmitterProfileErrors.NotConfigured
            : profile;
    }
}
