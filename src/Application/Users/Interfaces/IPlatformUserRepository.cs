using NovaFE.Domain.Users;

namespace NovaFE.Application.Users.Interfaces;

/// <summary>Write side (EF Core) de los <see cref="PlatformUser"/>.</summary>
public interface IPlatformUserRepository
{
    /// <summary>El usuario <paramref name="id"/>, o <c>null</c>.</summary>
    Task<PlatformUser?> GetAsync(Guid id, CancellationToken ct = default);

    /// <summary>El usuario con ese correo (normalizado), o <c>null</c>. Para el chequeo de alta.</summary>
    Task<PlatformUser?> GetByEmailAsync(string email, CancellationToken ct = default);

    Task AddAsync(PlatformUser user, CancellationToken ct = default);

    Task UpdateAsync(PlatformUser user, CancellationToken ct = default);

    /// <summary>
    /// Enlaza la cuenta de Better Auth sin cargar la entidad ni tocar su auditoría.
    /// Lo llama el handler de autenticación en el primer login, best-effort.
    /// </summary>
    Task LinkAuthUserAsync(Guid id, string authUserId, CancellationToken ct = default);
}
