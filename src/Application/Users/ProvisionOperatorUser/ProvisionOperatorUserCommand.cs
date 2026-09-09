namespace NovaFE.Application.Users.ProvisionOperatorUser;

/// <summary>
/// Da de alta a un operador del SaaS en el dashboard (rol <c>admin_sistema</c>,
/// sin contribuyente). El primer operador se aprovisiona con el rompe-cristal
/// <c>X-Admin-Key</c>; los siguientes, ya con la sesión de un operador.
/// </summary>
public sealed record ProvisionOperatorUserCommand(string Email);
