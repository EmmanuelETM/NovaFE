using ErrorOr;
using NovaFE.Application.Common;
using NovaFE.Application.Common.Interfaces;
using NovaFE.Application.Dgii.Contracts;
using NovaFE.Application.Dgii.Interfaces;
using NovaFE.Application.Ecf.Interfaces;
using NovaFE.Application.Tenants.Interfaces;
using NovaFE.Domain.Common;
using NovaFE.Domain.Ecf;
using Microsoft.Extensions.Logging;

namespace NovaFE.Application.Ecf.GetEcfTrackIds;

/// <summary>Todos los trackIds que la DGII tiene registrados para un e-NCF (Módulo 10).</summary>
public sealed record GetEcfTrackIdsQuery(Guid Id);

/// <summary>
/// Consulta <c>consultatrackids</c> contra la DGII para el comprobante — puede
/// haber más de un trackId si se remitió varias veces. Complementa el último
/// <c>track_id</c> que ya guarda <c>issued_ecf</c>. No disponible en CerteCF
/// (<see cref="IDgiiQueryClient"/> lo rechaza sin llamar).
/// </summary>
public sealed class GetEcfTrackIdsUseCase(
    ILoggerFactory loggerFactory,
    ICurrentTenant currentTenant,
    IEcfRepository ecf,
    ITenantRepository tenants,
    IDgiiTokenProvider tokenProvider,
    IDgiiQueryClient queryClient)
    : QueryUseCase<GetEcfTrackIdsQuery, IReadOnlyList<DgiiTrackIdEntry>>(loggerFactory)
{
    protected override async Task<ErrorOr<IReadOnlyList<DgiiTrackIdEntry>>> ExecuteCore(
        GetEcfTrackIdsQuery request, CancellationToken ct)
    {
        if (currentTenant.TenantId is not { } tenantId)
            return Errors.Auth.TenantNotResolved;

        var issued = await ecf.GetByIdAsync(request.Id, ct);
        if (issued is null)
            return EcfErrors.NotFound(request.Id);

        var tenant = await tenants.GetByIdAsync(tenantId, ct);
        if (tenant is null)
            return Errors.Auth.TenantNotResolved;

        var token = await tokenProvider.GetTokenAsync(issued.Environment, ct);
        if (token.IsError)
            return token.Errors;

        return await queryClient.GetTrackIdsAsync(
            issued.Environment, token.Value.Value, tenant.Rnc.Value, issued.Encf.Value, ct);
    }
}
