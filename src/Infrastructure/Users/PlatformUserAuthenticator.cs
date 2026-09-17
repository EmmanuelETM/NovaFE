using Microsoft.Extensions.Logging;
using NovaFE.Application.Users.Interfaces;
using NovaFE.Domain.Users;

namespace NovaFE.Infrastructure.Users;

/// <summary>
/// Resuelve al usuario humano del dashboard. Busca por el id opaco de Better Auth
/// y, si no da resultado, por correo (usuario recién aprovisionado que entra por
/// primera vez) — y en ese caso enlaza la cuenta para que el próximo lookup sea
/// por id.
/// <para>
/// Fase 2: el tenant/rol efectivo ya <b>no</b> sale de
/// <c>PlatformUser.TenantId</c>/<c>Role</c> para un usuario de contribuyente —
/// esos campos quedan vestigiales para ese caso (ver
/// <see cref="Domain.Users.PlatformUser.CreateOrganizationMember"/>). Se
/// resuelven en cada login vía <c>tenant_members</c>/<c>organization_members</c>,
/// para el tenant pedido (<c>X-Acting-Tenant-Id</c>) o uno por defecto.
/// </para>
/// </summary>
internal sealed class PlatformUserAuthenticator(
    IPlatformUserReadRepository readRepository,
    IPlatformUserRepository writeRepository,
    ILogger<PlatformUserAuthenticator> logger)
    : IPlatformUserAuthenticator
{
    public async Task<PlatformUserIdentity?> AuthenticateAsync(
        string? authUserId,
        string email,
        Guid? actingTenantId,
        CancellationToken ct = default)
    {
        var trimmedAuthId = string.IsNullOrWhiteSpace(authUserId) ? null : authUserId.Trim();

        var lookup = await readRepository.ResolveAsync(trimmedAuthId, email, ct);
        if (lookup is null || lookup.RevokedAt is not null)
            return null;

        // El correo cae en un usuario ya enlazado a OTRA cuenta de autenticación:
        // la sesión que llega no es de esa persona. Se rechaza.
        if (lookup.AuthUserId is not null
            && trimmedAuthId is not null
            && !string.Equals(lookup.AuthUserId, trimmedAuthId, StringComparison.Ordinal))
        {
            logger.LogWarning(
                "El correo {Email} corresponde a un usuario ya enlazado a otra cuenta; se rechaza el acceso.",
                lookup.Email);
            return null;
        }

        // Primer login: enlaza la cuenta de Better Auth. Best-effort; el índice
        // único parcial y el WHERE auth_user_id IS NULL hacen segura la carrera.
        if (lookup.AuthUserId is null && trimmedAuthId is not null)
        {
            try
            {
                await writeRepository.LinkAuthUserAsync(lookup.Id, trimmedAuthId, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "No se pudo enlazar la cuenta de autenticación del usuario {UserId}.", lookup.Id);
            }
        }

        // El operador no es miembro de tenant_members/organization_members:
        // su rol es fijo, sin tenant.
        if (string.Equals(lookup.Role, PlatformRole.AdminSistema.Name, StringComparison.Ordinal))
            return new PlatformUserIdentity(lookup.Id, null, lookup.Role, lookup.Email);

        if (actingTenantId is { } tenantId)
        {
            var access = await readRepository.ResolveTenantAccessAsync(lookup.Id, tenantId, ct);
            return access is null
                ? null // Pidió explícito un tenant al que no llega (o suspendido): se rechaza, no se cae a un default.
                : new PlatformUserIdentity(lookup.Id, access.TenantId, access.Role, lookup.Email);
        }

        var defaultAccess = await readRepository.ResolveDefaultTenantAccessAsync(lookup.Id, ct);
        return new PlatformUserIdentity(
            lookup.Id,
            defaultAccess?.TenantId,
            defaultAccess?.Role ?? PlatformRole.Consultor.Name,
            lookup.Email);
    }
}
