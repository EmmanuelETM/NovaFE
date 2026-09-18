using ErrorOr;

namespace NovaFE.Domain.Organizations;

/// <summary>
/// Errores de negocio del módulo Organizations. Los <c>code</c> son
/// identificadores estables (inglés); las descripciones las consume quien llama
/// a la API, por eso van en español.
/// </summary>
public static class OrganizationErrors
{
    public static Error NotAllowed => Error.Forbidden(
        code: "Organization.NotAllowed",
        description: "No tienes permiso para administrar los miembros de esta organización.");

    public static Error NotFound(Guid id) => Error.NotFound(
        code: "Organization.NotFound",
        description: $"No existe una organización con id '{id}'.");

    public static Error SlugAlreadyRegistered(string slug) => Error.Conflict(
        code: "Organization.SlugAlreadyRegistered",
        description: $"Ya hay una organización registrada con el slug '{slug}'.");

    public static Error UnknownRole(string role) => Error.Validation(
        code: "Organization.UnknownRole",
        description: $"Rol de organización desconocido: '{role}'.");

    public static Error UnknownPlan(string plan) => Error.Validation(
        code: "Organization.UnknownPlan",
        description: $"Plan desconocido: '{plan}'.");

    public static Error AlreadySuspended => Error.Conflict(
        code: "Organization.AlreadySuspended",
        description: "La organización ya estaba suspendida.");

    public static Error NotSuspended => Error.Conflict(
        code: "Organization.NotSuspended",
        description: "La organización no está suspendida, no hay nada que reactivar.");

    public static Error MemberNotFound(Guid platformUserId) => Error.NotFound(
        code: "Organization.MemberNotFound",
        description: $"El usuario '{platformUserId}' no es miembro de esta organización.");

    public static Error MemberAlreadyExists(string email) => Error.Conflict(
        code: "Organization.MemberAlreadyExists",
        description: $"El usuario con correo '{email}' ya es miembro de esta organización.");

    public static Error CannotRemoveLastOwner => Error.Conflict(
        code: "Organization.CannotRemoveLastOwner",
        description: "No puedes quitar o degradar al último owner de la organización.");
}
