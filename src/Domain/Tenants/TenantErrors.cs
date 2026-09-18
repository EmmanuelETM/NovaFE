using ErrorOr;

namespace NovaFE.Domain.Tenants;

/// <summary>
/// Errores de negocio del módulo Tenants. Los <c>code</c> son identificadores
/// estables (inglés); las descripciones las consume quien llama a la API, por eso
/// van en español.
/// </summary>
public static class TenantErrors
{
    public static Error NotFound(Guid id) => Error.NotFound(
        code: "Tenant.NotFound",
        description: $"No existe un contribuyente con id '{id}'.");

    public static Error RncAlreadyRegistered(string rnc) => Error.Conflict(
        code: "Tenant.RncAlreadyRegistered",
        description: $"Ya hay un contribuyente registrado con el RNC '{rnc}'.");

    public static Error AlreadySuspended => Error.Conflict(
        code: "Tenant.AlreadySuspended",
        description: "El contribuyente ya estaba suspendido.");

    public static Error NotSuspended => Error.Conflict(
        code: "Tenant.NotSuspended",
        description: "El contribuyente no está suspendido, no hay nada que reactivar.");

    public static Error NotOwnedByOrganization(Guid tenantId, Guid organizationId) => Error.Conflict(
        code: "Tenant.NotOwnedByOrganization",
        description: $"El contribuyente '{tenantId}' no pertenece a la organización '{organizationId}'.");

    public static Error AlreadyAssignedToOrganization(Guid tenantId, Guid organizationId) => Error.Conflict(
        code: "Tenant.AlreadyAssignedToOrganization",
        description: $"El contribuyente '{tenantId}' ya pertenece a otra organización. Desasócialo primero.");
}
