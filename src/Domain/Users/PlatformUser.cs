using ErrorOr;
using NovaFE.Domain.Common.Entities;

namespace NovaFE.Domain.Users;

/// <summary>
/// Usuario humano de la plataforma (dashboard). Lo administra el operador del SaaS
/// (igual que <see cref="Tenants.ApiKey"/>: <b>no</b> es <c>ITenantOwned</c>,
/// <b>no</b> lleva RLS — la resolución de identidad ocurre antes de que haya
/// tenant en la petición, y un operador no tiene tenant).
/// <para>
/// La <b>identidad</b> (correo, contraseña, sesión) la maneja Better Auth en el
/// dashboard; esta entidad es solo la <b>autorización</b>: a qué contribuyente y
/// con qué rol. Se da de alta por <see cref="Email"/>; el <see cref="AuthUserId"/>
/// (el id opaco de Better Auth) se enlaza en el primer login.
/// </para>
/// </summary>
public sealed class PlatformUser : Entity<Guid>, IAuditableEntity, ISoftDeletable
{
    /// <summary>Largo máximo del correo y del id de la cuenta de autenticación.</summary>
    public const int MaxEmailLength = 256;

    /// <summary>Largo máximo del id opaco de Better Auth.</summary>
    public const int MaxAuthUserIdLength = 128;

    // Required by EF Core.
    private PlatformUser()
    {
    }

    private PlatformUser(Guid id, string email, Guid? tenantId, PlatformRole role)
        : base(id)
    {
        Email = email;
        TenantId = tenantId;
        Role = role;
    }

    /// <summary>Correo del usuario, normalizado a minúsculas. Único: es la clave de alta.</summary>
    public string Email { get; private set; } = null!;

    /// <summary>
    /// Id opaco de la cuenta de Better Auth. <c>null</c> hasta el primer login,
    /// cuando el handler de autenticación lo enlaza por correo.
    /// </summary>
    public string? AuthUserId { get; private set; }

    /// <summary>El contribuyente al que pertenece; <c>null</c> = operador del SaaS.</summary>
    public Guid? TenantId { get; private set; }

    /// <summary>Rol del usuario. Determina qué políticas de la API puede pasar.</summary>
    public PlatformRole Role { get; private set; } = null!;

    /// <summary>Momento de revocación; <c>null</c> = vigente.</summary>
    public DateTimeOffset? RevokedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }

    /// <summary>Sirve para autenticar: no revocado. La expiración de sesión la maneja Better Auth.</summary>
    public bool IsUsable => RevokedAt is null;

    /// <summary>Da de alta a un operador del SaaS (rol <c>admin_sistema</c>, sin tenant).</summary>
    public static ErrorOr<PlatformUser> CreateOperator(string email)
    {
        var normalized = NormalizeEmail(email);
        if (normalized.IsError)
            return normalized.Errors;

        return new PlatformUser(Guid.CreateVersion7(), normalized.Value, tenantId: null, PlatformRole.AdminSistema);
    }

    /// <summary>
    /// Da de alta a un usuario de un contribuyente. Rechaza <c>admin_sistema</c>
    /// (ese rol es exclusivo del operador).
    /// </summary>
    public static ErrorOr<PlatformUser> CreateTenantUser(string email, Guid tenantId, PlatformRole role)
    {
        ArgumentNullException.ThrowIfNull(role);

        if (tenantId == Guid.Empty)
            return PlatformUserErrors.TenantRequired;

        if (!role.IsTenantRole)
            return PlatformUserErrors.InvalidRoleForTenant;

        var normalized = NormalizeEmail(email);
        if (normalized.IsError)
            return normalized.Errors;

        return new PlatformUser(Guid.CreateVersion7(), normalized.Value, tenantId, role);
    }

    /// <summary>
    /// Enlaza la cuenta de Better Auth en el primer login. Idempotente si es el
    /// mismo id; error si ya está enlazado a otro.
    /// </summary>
    public ErrorOr<Success> LinkAuthUser(string authUserId)
    {
        if (string.IsNullOrWhiteSpace(authUserId))
            return PlatformUserErrors.AuthUserAlreadyLinked;

        var clean = authUserId.Trim();

        if (AuthUserId is not null)
            return string.Equals(AuthUserId, clean, StringComparison.Ordinal)
                ? Result.Success
                : PlatformUserErrors.AuthUserAlreadyLinked;

        AuthUserId = clean;
        return Result.Success;
    }

    /// <summary>Revoca el acceso. Idempotencia estricta: revocar dos veces es un error.</summary>
    public ErrorOr<Success> Revoke(DateTimeOffset at)
    {
        if (RevokedAt is not null)
            return PlatformUserErrors.AlreadyRevoked;

        RevokedAt = at;
        return Result.Success;
    }

    /// <summary>
    /// Reactiva a un usuario revocado. Idempotencia estricta (espejo de
    /// <see cref="Revoke"/>): reactivar a uno que no estaba revocado es un error.
    /// </summary>
    public ErrorOr<Success> Reinstate()
    {
        if (RevokedAt is null)
            return PlatformUserErrors.NotRevoked;

        RevokedAt = null;
        return Result.Success;
    }

    /// <summary>
    /// Cambia el rol. Solo para usuarios de un contribuyente: <c>admin_sistema</c>
    /// es exclusivo del operador y no se puede asignar acá. Cambiarlo por el mismo
    /// rol es un no-op. Se permite aunque el usuario esté revocado (no afecta el acceso).
    /// </summary>
    public ErrorOr<Success> ChangeRole(PlatformRole newRole)
    {
        ArgumentNullException.ThrowIfNull(newRole);

        if (!newRole.IsTenantRole)
            return PlatformUserErrors.InvalidRoleForTenant;

        Role = newRole;
        return Result.Success;
    }

    private static ErrorOr<string> NormalizeEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return PlatformUserErrors.EmailRequired;

        var trimmed = email.Trim().ToLowerInvariant();

        if (trimmed.Length > MaxEmailLength)
            return PlatformUserErrors.InvalidEmail;

        var at = trimmed.IndexOf('@', StringComparison.Ordinal);
        if (at <= 0 || at != trimmed.LastIndexOf('@')
            || at == trimmed.Length - 1
            || !trimmed[(at + 1)..].Contains('.', StringComparison.Ordinal)
            || trimmed.Any(char.IsWhiteSpace))
        {
            return PlatformUserErrors.InvalidEmail;
        }

        return trimmed;
    }
}
