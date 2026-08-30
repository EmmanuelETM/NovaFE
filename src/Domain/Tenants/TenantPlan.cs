using NovaFE.Domain.Common;

namespace NovaFE.Domain.Tenants;

/// <summary>
/// Nivel comercial de un <see cref="Tenant"/>. <see cref="Enumeration{T}.Name"/>
/// es la clave interna (inglés, lo que se persiste); <see cref="DisplayName"/> es
/// el nombre comercial en español que ve el cliente. Las cuotas y tarifas por
/// nivel se modelarán en el módulo de facturación (ver <c>docs/pricing.md</c>).
/// </summary>
public sealed record TenantPlan(int Id, string Name, string DisplayName)
    : Enumeration<TenantPlan>(Id, Name)
{
    /// <summary>Embudo gratuito: ~100 e-CF/mes, casi todo TestECF.</summary>
    public static readonly TenantPlan Developer = new(1, nameof(Developer), "Developer");

    /// <summary>Negocios pequeños: ~400 e-CF/mes.</summary>
    public static readonly TenantPlan Starter = new(2, nameof(Starter), "Emprendedor");

    /// <summary>PyMEs: ~2,500 e-CF/mes, SLA 99.5%.</summary>
    public static readonly TenantPlan Business = new(3, nameof(Business), "Negocio");

    /// <summary>Grandes y medianos contribuyentes: ~20,000 e-CF/mes, SLA 99.9%.</summary>
    public static readonly TenantPlan Corporate = new(4, nameof(Corporate), "Corporativo");

    /// <summary>Alto volumen / white-label: cuota y SLA a convenir.</summary>
    public static readonly TenantPlan Enterprise = new(5, nameof(Enterprise), "Empresarial");
}
