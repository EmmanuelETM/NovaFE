using ErrorOr;
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

    private Tenant(Guid id, Rnc rnc, string legalName, string? tradeName)
        : base(id)
    {
        Rnc = rnc;
        LegalName = legalName;
        TradeName = tradeName;
        Status = TenantStatus.Active;
    }

    /// <summary>RNC del contribuyente. Único en la plataforma.</summary>
    public Rnc Rnc { get; private set; }

    /// <summary>Razón social registrada.</summary>
    public string LegalName { get; private set; } = null!;

    /// <summary>Nombre comercial. Opcional.</summary>
    public string? TradeName { get; private set; }

    public TenantStatus Status { get; private set; } = null!;

    /// <summary>
    /// La organización que agrupa a este contribuyente en la jerarquía
    /// <c>User -&gt; Organization -&gt; Tenant</c>. Nullable durante la
    /// transición (Fase 1): un tenant existente todavía sin backfillear no
    /// tiene organización hasta que se le asigne una. El plan/cuota vive en
    /// <see cref="Organizations.Organization.Plan"/>, no acá — la facturación
    /// se consolida a nivel organización (Fase 2).
    /// </summary>
    public Guid? OrganizationId { get; private set; }

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
    public static Tenant Register(Rnc rnc, string legalName, string? tradeName)
    {
        var trimmedTradeName = string.IsNullOrWhiteSpace(tradeName) ? null : tradeName.Trim();

        return new Tenant(Guid.CreateVersion7(), rnc, legalName.Trim(), trimmedTradeName);
    }

    /// <summary>Reactiva un tenant suspendido. Idempotencia estricta: reactivar uno activo es un error.</summary>
    public ErrorOr<Success> Activate()
    {
        if (Status == TenantStatus.Active)
            return TenantErrors.NotSuspended;

        Status = TenantStatus.Active;
        return Result.Success;
    }

    /// <summary>Suspende el tenant — bloquea autenticación y emisión. Idempotencia estricta.</summary>
    public ErrorOr<Success> Suspend()
    {
        if (Status == TenantStatus.Suspended)
            return TenantErrors.AlreadySuspended;

        Status = TenantStatus.Suspended;
        return Result.Success;
    }

    /// <summary>Utilizable: activo. Ni el tenant ni (si aplica) su organización dueña están suspendidos.</summary>
    public bool IsUsable(bool organizationSuspended) => Status == TenantStatus.Active && !organizationSuspended;

    /// <summary>Asigna (o reasigna) la organización dueña de este contribuyente.</summary>
    public void AssignToOrganization(Guid organizationId) => OrganizationId = organizationId;
}
