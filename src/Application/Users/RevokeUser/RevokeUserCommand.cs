namespace NovaFE.Application.Users.RevokeUser;

/// <summary>
/// Revoca el acceso de un usuario del dashboard. Recurso de operador.
/// <see cref="TenantScope"/> acota qué usuario se puede revocar por cada ruta:
/// el id del contribuyente para <c>/tenants/{id}/users/{userId}</c>, o
/// <c>null</c> para <c>/operator-users/{userId}</c> (solo operadores).
/// </summary>
public sealed record RevokeUserCommand(Guid UserId, Guid? TenantScope);
