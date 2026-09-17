using NovaFE.Domain.Common.Entities;

namespace NovaFE.Domain.Organizations;

/// <summary>
/// Relación N:M entre un <see cref="Domain.Users.PlatformUser"/> y una
/// <see cref="Organization"/>, con el rol del usuario en esa organización. Un
/// mismo usuario puede tener una fila por cada organización a la que pertenece
/// (contadores/agencias que administran varios contribuyentes).
/// <para>
/// No es <c>ITenantOwned</c>: no es dato de un tenant, es el mapa de a qué
/// organizaciones llega un usuario. Se filtra explícito por repositorio, igual
/// que <c>platform_users</c>.
/// </para>
/// </summary>
public sealed class OrganizationMember : Entity<Guid>, IAuditableEntity
{
    // Required by EF Core.
    private OrganizationMember()
    {
    }

    private OrganizationMember(Guid id, Guid organizationId, Guid platformUserId, OrganizationRole role)
        : base(id)
    {
        OrganizationId = organizationId;
        PlatformUserId = platformUserId;
        Role = role;
    }

    public Guid OrganizationId { get; private set; }

    public Guid PlatformUserId { get; private set; }

    public OrganizationRole Role { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    public static OrganizationMember Create(Guid organizationId, Guid platformUserId, OrganizationRole role)
        => new(Guid.CreateVersion7(), organizationId, platformUserId, role);

    public void ChangeRole(OrganizationRole newRole) => Role = newRole;
}
