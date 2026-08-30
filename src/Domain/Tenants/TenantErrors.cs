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

    public static Error UnknownPlan(string plan) => Error.Validation(
        code: "Tenant.UnknownPlan",
        description: $"Plan desconocido: '{plan}'.");
}
