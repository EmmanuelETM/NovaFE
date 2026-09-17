using NovaFE.Domain.Common;

namespace NovaFE.Domain.Organizations;

/// <summary>
/// Nivel comercial de una <see cref="Organization"/>. <see cref="Enumeration{T}.Name"/>
/// es la clave interna (inglés, lo que se persiste); <see cref="DisplayName"/> es
/// el nombre comercial en español que ve el cliente. Vive en Organization, no en
/// Tenant: la facturación se consolida a nivel organización (Fase 2, ver
/// <c>docs/multi-tenancy-hierarchy.md</c>). Las cuotas y tarifas por nivel se
/// modelarán en el módulo de facturación (ver <c>docs/pricing.md</c>).
/// </summary>
public sealed record OrganizationPlan(int Id, string Name, string DisplayName)
    : Enumeration<OrganizationPlan>(Id, Name)
{
    /// <summary>Embudo gratuito: ~100 e-CF/mes, casi todo TestECF.</summary>
    public static readonly OrganizationPlan Developer = new(1, nameof(Developer), "Developer");

    /// <summary>Negocios pequeños: ~400 e-CF/mes.</summary>
    public static readonly OrganizationPlan Starter = new(2, nameof(Starter), "Emprendedor");

    /// <summary>PyMEs: ~2,500 e-CF/mes, SLA 99.5%.</summary>
    public static readonly OrganizationPlan Business = new(3, nameof(Business), "Negocio");

    /// <summary>Grandes y medianos contribuyentes: ~20,000 e-CF/mes, SLA 99.9%.</summary>
    public static readonly OrganizationPlan Corporate = new(4, nameof(Corporate), "Corporativo");

    /// <summary>Alto volumen / white-label: cuota y SLA a convenir.</summary>
    public static readonly OrganizationPlan Enterprise = new(5, nameof(Enterprise), "Empresarial");
}
