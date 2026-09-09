namespace NovaFE.Application.Users.ProvisionTenantUser;

/// <summary>
/// Da de alta a un empleado de un contribuyente en el dashboard. Recurso de
/// operador: <see cref="TenantId"/> viene de la ruta. Se aprovisiona por correo;
/// la persona podrá entrar cuando inicie sesión con ese mismo correo en Better
/// Auth (Google/GitHub/Microsoft o email+contraseña).
/// </summary>
public sealed record ProvisionTenantUserCommand(Guid TenantId, string Email, string Role);
