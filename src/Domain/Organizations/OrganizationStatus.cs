using NovaFE.Domain.Common;

namespace NovaFE.Domain.Organizations;

/// <summary>
/// Lifecycle state of an <see cref="Organization"/>. Persisted by name. Mismo
/// concepto que <see cref="Tenants.TenantStatus"/> pero a nivel organización:
/// suspender una organización bloquea a <b>todos</b> sus tenants (falta de
/// pago, abuso, requerimiento legal), en cascada — ver <c>docs/multi-tenancy-hierarchy.md</c>.
/// </summary>
public sealed record OrganizationStatus(int Id, string Name) : Enumeration<OrganizationStatus>(Id, Name)
{
    public static readonly OrganizationStatus Active = new(1, nameof(Active));

    public static readonly OrganizationStatus Suspended = new(2, nameof(Suspended));
}
