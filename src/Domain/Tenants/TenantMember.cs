using ErrorOr;
using NovaFE.Domain.Common.Entities;
using NovaFE.Domain.Users;

namespace NovaFE.Domain.Tenants;

/// <summary>
/// Relación N:M entre un <see cref="Domain.Users.PlatformUser"/> y un
/// <see cref="Tenant"/> (el "proyecto" de la jerarquía
/// <c>User -&gt; Organization -&gt; Tenant</c>), con el rol del usuario en ese
/// tenant. Reemplaza, a futuro, los campos únicos <c>PlatformUser.TenantId</c> /
/// <c>PlatformUser.Role</c> por una relación real N:M: un usuario de una
/// organización con varios contribuyentes puede operar más de uno, con roles
/// distintos en cada uno.
/// <para>
/// <b>Transición (Fase 1):</b> por ahora coexiste con
/// <see cref="PlatformUser.TenantId"/>/<see cref="PlatformUser.Role"/> — el
/// backfill de esta fase espeja esos campos acá, pero el flujo de autenticación
/// (<c>InternalKeyAuthenticationHandler</c>, <c>GetCurrentUserUseCase</c>)
/// todavía lee de <c>PlatformUser</c>. Cortar esa lectura hacia
/// <see cref="TenantMember"/> es trabajo de la Fase 2 (ver
/// <c>docs/multi-tenancy-hierarchy.md</c>).
/// </para>
/// <para>
/// No es <c>ITenantOwned</c> en el sentido de "dato de negocio del tenant": es
/// el mapa de a qué tenants llega un usuario. Se filtra explícito por
/// repositorio, no por RLS.
/// </para>
/// </summary>
public sealed class TenantMember : Entity<Guid>, IAuditableEntity
{
    // Required by EF Core.
    private TenantMember()
    {
    }

    private TenantMember(Guid id, Guid tenantId, Guid platformUserId, PlatformRole role)
        : base(id)
    {
        TenantId = tenantId;
        PlatformUserId = platformUserId;
        Role = role;
    }

    /// <summary>El tenant ("proyecto") al que pertenece esta membresía.</summary>
    public Guid TenantId { get; private set; }

    public Guid PlatformUserId { get; private set; }

    /// <summary>Rol del usuario en este tenant. Nunca <c>admin_sistema</c> (exclusivo del operador).</summary>
    public PlatformRole Role { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    /// <summary>Rechaza <c>admin_sistema</c>: ese rol es exclusivo del operador, nunca de un tenant.</summary>
    public static ErrorOr<TenantMember> Create(Guid tenantId, Guid platformUserId, PlatformRole role)
    {
        ArgumentNullException.ThrowIfNull(role);

        if (!role.IsTenantRole)
            return PlatformUserErrors.InvalidRoleForTenant;

        return new TenantMember(Guid.CreateVersion7(), tenantId, platformUserId, role);
    }

    /// <summary>Rechaza <c>admin_sistema</c>, mismo motivo que <see cref="Create"/>.</summary>
    public ErrorOr<Success> ChangeRole(PlatformRole newRole)
    {
        ArgumentNullException.ThrowIfNull(newRole);

        if (!newRole.IsTenantRole)
            return PlatformUserErrors.InvalidRoleForTenant;

        Role = newRole;
        return Result.Success;
    }
}
