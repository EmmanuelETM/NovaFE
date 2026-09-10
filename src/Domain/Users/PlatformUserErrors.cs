using ErrorOr;

namespace NovaFE.Domain.Users;

/// <summary>
/// Errores de negocio de los usuarios de la plataforma. <c>code</c> en inglés
/// (estable); la descripción la consume quien llama a la API, por eso va en español.
/// </summary>
public static class PlatformUserErrors
{
    public static Error EmailRequired => Error.Validation(
        code: "PlatformUser.EmailRequired",
        description: "El correo del usuario es obligatorio.");

    public static Error InvalidEmail => Error.Validation(
        code: "PlatformUser.InvalidEmail",
        description: "El correo del usuario no tiene un formato válido.");

    public static Error EmailAlreadyProvisioned(string email) => Error.Conflict(
        code: "PlatformUser.EmailAlreadyProvisioned",
        description: $"Ya existe un usuario dado de alta con el correo '{email}'.");

    public static Error TenantRequired => Error.Validation(
        code: "PlatformUser.TenantRequired",
        description: "El usuario de un contribuyente debe indicar a qué contribuyente pertenece.");

    public static Error InvalidRoleForTenant => Error.Validation(
        code: "PlatformUser.InvalidRoleForTenant",
        description: "El rol 'admin_sistema' es exclusivo del operador; un usuario de contribuyente no puede tenerlo.");

    public static Error NotFound(Guid id) => Error.NotFound(
        code: "PlatformUser.NotFound",
        description: $"No existe un usuario con id '{id}'.");

    public static Error AlreadyRevoked => Error.Conflict(
        code: "PlatformUser.AlreadyRevoked",
        description: "El usuario ya estaba revocado.");

    public static Error NotRevoked => Error.Conflict(
        code: "PlatformUser.NotRevoked",
        description: "El usuario no está revocado, no hay nada que reactivar.");

    public static Error AuthUserAlreadyLinked => Error.Conflict(
        code: "PlatformUser.AuthUserAlreadyLinked",
        description: "El usuario ya está enlazado a otra cuenta de autenticación.");
}
