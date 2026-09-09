namespace NovaFE.Application.Users.Contracts;

/// <summary>Vista de un usuario de la plataforma para los listados del operador.</summary>
public sealed record PlatformUserDto(
    Guid Id,
    string Email,
    string Role,
    Guid? TenantId,
    bool AuthLinked,
    DateTimeOffset? RevokedAt,
    DateTimeOffset CreatedAt)
{
    /// <summary>Vigente: no revocado.</summary>
    public bool IsActive => RevokedAt is null;
}
