using Microsoft.Extensions.Logging;
using NovaFE.Application.Users.Interfaces;

namespace NovaFE.Infrastructure.Users;

/// <summary>
/// Resuelve al usuario humano del dashboard. Busca por el id opaco de Better Auth
/// y, si no da resultado, por correo (usuario recién aprovisionado que entra por
/// primera vez) — y en ese caso enlaza la cuenta para que el próximo lookup sea
/// por id.
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

        return new PlatformUserIdentity(lookup.Id, lookup.TenantId, lookup.Role, lookup.Email);
    }
}
