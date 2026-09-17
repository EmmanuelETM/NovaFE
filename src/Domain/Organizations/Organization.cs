using ErrorOr;
using NovaFE.Domain.Common.Entities;

namespace NovaFE.Domain.Organizations;

/// <summary>
/// Agrupa uno o más <see cref="Tenants.Tenant"/> (contribuyentes/RNC) bajo una
/// misma cuenta. Es el nivel <b>arriba</b> de <c>Tenant</c> en la jerarquía
/// <c>User -&gt; Organization -&gt; Tenant</c>: la cuenta pagadora (plan,
/// suscripción, métricas de uso consolidadas) y el punto de entrada de un
/// usuario que puede operar más de un contribuyente (un contador, una agencia).
/// <para>
/// No es <see cref="Domain.Common.Entities.ITenantOwned"/>: no pertenece a un
/// tenant, es quien los agrupa. No lleva RLS — el aislamiento de quién ve qué
/// organización lo resuelve la capa de aplicación a través de
/// <see cref="OrganizationMember"/>, igual que <c>platform_users</c> hoy.
/// </para>
/// </summary>
public sealed class Organization : Entity<Guid>, IAuditableEntity, ISoftDeletable
{
    /// <summary>Largo máximo del nombre.</summary>
    public const int MaxNameLength = 150;

    /// <summary>Largo máximo del slug.</summary>
    public const int MaxSlugLength = 63;

    // Required by EF Core.
    private Organization()
    {
    }

    private Organization(Guid id, string name, string slug, OrganizationPlan plan)
        : base(id)
    {
        Name = name;
        Slug = slug;
        Plan = plan;
        Status = OrganizationStatus.Active;
    }

    /// <summary>Nombre visible de la organización.</summary>
    public string Name { get; private set; } = null!;

    /// <summary>
    /// Identificador corto y único para URLs (<c>[orgSlug]</c> del dashboard).
    /// Minúsculas, dígitos y guiones — la forma exacta la valida el comando.
    /// </summary>
    public string Slug { get; private set; } = null!;

    /// <summary>Nivel comercial / cuota. La bolsa de comprobantes se consolida acá, no por Tenant.</summary>
    public OrganizationPlan Plan { get; private set; } = null!;

    public OrganizationStatus Status { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }

    /// <summary>
    /// Registra una nueva organización. La unicidad del slug es un chequeo de
    /// repositorio (necesita E/S), resuelto antes de llamar acá.
    /// </summary>
    public static Organization Register(string name, string slug, OrganizationPlan plan)
        => new(Guid.CreateVersion7(), name.Trim(), slug.Trim().ToLowerInvariant(), plan);

    public void Rename(string name) => Name = name.Trim();

    /// <summary>
    /// Corrección manual del plan por el operador — no hay pasarela de pago
    /// todavía, así que no hay invariante que romper (a diferencia de
    /// <see cref="Suspend"/>/<see cref="Activate"/>, que sí tienen estado
    /// previo que validar).
    /// </summary>
    public void ChangePlan(OrganizationPlan plan) => Plan = plan;

    /// <summary>Reactiva una organización suspendida. Idempotencia estricta.</summary>
    public ErrorOr<Success> Activate()
    {
        if (Status == OrganizationStatus.Active)
            return OrganizationErrors.NotSuspended;

        Status = OrganizationStatus.Active;
        return Result.Success;
    }

    /// <summary>Suspende la organización. Bloquea en cascada a todos sus tenants (ver <c>Tenant.IsUsable</c>). Idempotencia estricta.</summary>
    public ErrorOr<Success> Suspend()
    {
        if (Status == OrganizationStatus.Suspended)
            return OrganizationErrors.AlreadySuspended;

        Status = OrganizationStatus.Suspended;
        return Result.Success;
    }
}
