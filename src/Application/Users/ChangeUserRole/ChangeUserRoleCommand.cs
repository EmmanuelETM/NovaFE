namespace NovaFE.Application.Users.ChangeUserRole;

/// <summary>
/// Cambia el rol de un usuario de un contribuyente. Recurso de operador.
/// <see cref="TenantScope"/> acota qué usuario se puede tocar por la ruta
/// (<c>/tenants/{id}/users/{userId}</c>). Los operadores del SaaS tienen un solo
/// rol, así que este comando no aplica a ellos.
/// </summary>
public sealed record ChangeUserRoleCommand(Guid UserId, Guid TenantScope, string Role);
