namespace NovaFE.Application.Users.ReinstateUser;

/// <summary>
/// Reactiva el acceso de un usuario revocado. Recurso de operador. Espejo de
/// <c>RevokeUserCommand</c>: <see cref="TenantScope"/> acota qué usuario se puede
/// tocar por cada ruta — el id del contribuyente para
/// <c>/tenants/{id}/users/{userId}/reinstate</c>, o <c>null</c> para
/// <c>/operator-users/{userId}/reinstate</c>.
/// </summary>
public sealed record ReinstateUserCommand(Guid UserId, Guid? TenantScope);
