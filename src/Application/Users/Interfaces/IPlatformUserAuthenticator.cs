namespace NovaFE.Application.Users.Interfaces;

/// <summary>
/// Resuelve la identidad que el BFF del dashboard afirma (<c>X-Acting-User</c> +
/// <c>X-Acting-Email</c>, ya autorizada por la internal key en la capa Service) a
/// un <see cref="PlatformUserIdentity"/>. La implementación (que toca la base y
/// enlaza la cuenta de Better Auth en el primer login) vive en Infrastructure.
/// </summary>
public interface IPlatformUserAuthenticator
{
    /// <summary>
    /// El usuario detrás de <paramref name="authUserId"/> / <paramref name="email"/>,
    /// o <c>null</c> si no está dado de alta, está revocado, el correo
    /// pertenece a un usuario ya enlazado a otra cuenta de autenticación, o
    /// <paramref name="actingTenantId"/> vino explícito y el usuario no tiene
    /// acceso a ese tenant (no existe, no es miembro, o está suspendido).
    /// </summary>
    /// <param name="actingTenantId">
    /// El tenant que el dashboard pide activar (<c>X-Acting-Tenant-Id</c>).
    /// <c>null</c> deja que se resuelva un tenant por defecto (Fase 2, ver
    /// <c>ResolveDefaultTenantAccessAsync</c>) — nunca falla la autenticación
    /// por sí solo, un usuario puede no tener tenant activo todavía.
    /// </param>
    Task<PlatformUserIdentity?> AuthenticateAsync(
        string? authUserId,
        string email,
        Guid? actingTenantId,
        CancellationToken ct = default);
}

/// <summary>Identidad resuelta de un usuario humano de la plataforma.</summary>
/// <param name="TenantId">El tenant activo; <c>null</c> = operador del SaaS, o ningún tenant disponible todavía.</param>
/// <param name="Role">El rol efectivo en <see cref="TenantId"/> (<c>admin_sistema</c> / <c>admin_tenant</c> / <c>emisor</c> / <c>consultor</c>).</param>
public sealed record PlatformUserIdentity(Guid UserId, Guid? TenantId, string Role, string Email);
