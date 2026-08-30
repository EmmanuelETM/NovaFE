using NovaFE.Domain.Common;
using NovaFE.Domain.Common.Entities;

namespace NovaFE.Domain.Tenants;

/// <summary>
/// Un contribuyente que emite e-CF a través de NovaFE. El tenant es la raíz de
/// aislamiento: cada tabla con datos de un cliente
/// (<see cref="ITenantOwned"/>) apunta a un <see cref="Tenant"/>.
/// <para>
/// Vive en el esquema compartido pero lo administra el operador del SaaS, no está
/// sujeto a RLS por tenant.
/// </para>
/// </summary>
public sealed class Tenant : Entity<Guid>, IAuditableEntity, ISoftDeletable
{
    // Required by EF Core.
    private Tenant()
    {
    }

    private Tenant(Guid id, Rnc rnc, string legalName, string? tradeName, TenantPlan plan)
        : base(id)
    {
        Rnc = rnc;
        LegalName = legalName;
        TradeName = tradeName;
        Plan = plan;
        Status = TenantStatus.Active;
    }

    /// <summary>RNC del contribuyente. Único en la plataforma.</summary>
    public Rnc Rnc { get; private set; }

    /// <summary>Razón social registrada.</summary>
    public string LegalName { get; private set; } = null!;

    /// <summary>Nombre comercial. Opcional.</summary>
    public string? TradeName { get; private set; }

    public TenantPlan Plan { get; private set; } = null!;

    public TenantStatus Status { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }

    /// <summary>
    /// Registers a new tenant. <paramref name="rnc"/> is assumed valid (build it
    /// with <see cref="Rnc.Create"/> in the use case). Uniqueness of the RNC is a
    /// repository concern, checked before this call.
    /// </summary>
    public static Tenant Register(Rnc rnc, string legalName, string? tradeName, TenantPlan plan)
    {
        var trimmedTradeName = string.IsNullOrWhiteSpace(tradeName) ? null : tradeName.Trim();

        return new Tenant(Guid.CreateVersion7(), rnc, legalName.Trim(), trimmedTradeName, plan);
    }

    public void Activate() => Status = TenantStatus.Active;

    public void Suspend() => Status = TenantStatus.Suspended;
}
