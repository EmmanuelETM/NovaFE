using ErrorOr;

namespace NovaFE.Domain.Tenants;

/// <summary>
/// Errores de negocio de las membresías de tenant (<see cref="TenantMember"/>).
/// </summary>
public static class TenantMemberErrors
{
    public static Error AlreadyMember(string email) => Error.Conflict(
        code: "TenantMember.AlreadyMember",
        description: $"El usuario con correo '{email}' ya tiene acceso a este contribuyente.");

    public static Error AlreadyRevoked => Error.Conflict(
        code: "TenantMember.AlreadyRevoked",
        description: "El usuario ya estaba revocado en este contribuyente.");

    public static Error NotRevoked => Error.Conflict(
        code: "TenantMember.NotRevoked",
        description: "El usuario no está revocado en este contribuyente, no hay nada que reactivar.");
}
