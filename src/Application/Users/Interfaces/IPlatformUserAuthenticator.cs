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
    /// o <c>null</c> si no está dado de alta, está revocado, o el correo pertenece
    /// a un usuario ya enlazado a otra cuenta de autenticación.
    /// </summary>
    Task<PlatformUserIdentity?> AuthenticateAsync(
        string? authUserId,
        string email,
        CancellationToken ct = default);
}

/// <summary>Identidad resuelta de un usuario humano de la plataforma.</summary>
/// <param name="TenantId">El contribuyente; <c>null</c> = operador del SaaS.</param>
/// <param name="Role">El rol (<c>admin_sistema</c> / <c>admin_tenant</c> / <c>emisor</c> / <c>consultor</c>).</param>
public sealed record PlatformUserIdentity(Guid UserId, Guid? TenantId, string Role, string Email);
