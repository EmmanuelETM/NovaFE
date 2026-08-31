using ErrorOr;
using NovaFE.Application.Common;
using NovaFE.Application.Tenants.Contracts;
using NovaFE.Application.Tenants.Interfaces;
using NovaFE.Domain.Tenants;
using Microsoft.Extensions.Logging;

namespace NovaFE.Application.Tenants.GetEmitterProfile;

public sealed class GetEmitterProfileUseCase(
    ILoggerFactory loggerFactory,
    IEmitterProfileReadRepository profiles)
    : QueryUseCase<GetEmitterProfileQuery, EmitterProfileDto>(loggerFactory)
{
    protected override async Task<ErrorOr<EmitterProfileDto>> ExecuteCore(
        GetEmitterProfileQuery request,
        CancellationToken ct)
    {
        var profile = await profiles.GetByTenantAsync(request.TenantId, ct);

        return profile is null
            ? EmitterProfileErrors.NotConfigured
            : profile;
    }
}
