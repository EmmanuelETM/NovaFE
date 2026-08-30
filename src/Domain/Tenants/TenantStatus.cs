using NovaFE.Domain.Common;

namespace NovaFE.Domain.Tenants;

/// <summary>
/// Lifecycle state of a <see cref="Tenant"/>. Persisted by name.
/// </summary>
public sealed record TenantStatus(int Id, string Name) : Enumeration<TenantStatus>(Id, Name)
{
    /// <summary>Can authenticate against DGII and emit e-CF.</summary>
    public static readonly TenantStatus Active = new(1, nameof(Active));

    /// <summary>Blocked from emitting — unpaid balance, compliance hold, or operator action.</summary>
    public static readonly TenantStatus Suspended = new(2, nameof(Suspended));
}
