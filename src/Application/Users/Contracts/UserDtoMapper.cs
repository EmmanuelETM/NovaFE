using NovaFE.Domain.Users;

namespace NovaFE.Application.Users.Contracts;

/// <summary>Proyecta un <see cref="PlatformUser"/> del dominio a su vista.</summary>
internal static class UserDtoMapper
{
    public static PlatformUserDto ToDto(PlatformUser user) => new(
        user.Id,
        user.Email,
        user.Role.Name,
        user.TenantId,
        user.AuthUserId is not null,
        user.RevokedAt,
        user.CreatedAt);
}
