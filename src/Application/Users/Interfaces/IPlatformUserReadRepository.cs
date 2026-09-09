using NovaFE.Application.Users.Contracts;

namespace NovaFE.Application.Users.Interfaces;

/// <summary>Read side (Dapper) de los usuarios de la plataforma.</summary>
public interface IPlatformUserReadRepository
{
    /// <summary>
    /// Resuelve al usuario para autenticar. Busca primero por
    /// <paramref name="authUserId"/> (si viene); si no da resultado, por
    /// <paramref name="email"/> (normalizado). Cruza tenants a propósito: todavía
    /// no hay tenant en la petición.
    /// </summary>
    Task<PlatformUserLookup?> ResolveAsync(string? authUserId, string email, CancellationToken ct = default);

    /// <summary>Los usuarios de un contribuyente, el más reciente primero.</summary>
    Task<IReadOnlyList<PlatformUserDto>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>Los operadores del SaaS (<c>tenant_id</c> nulo), el más reciente primero.</summary>
    Task<IReadOnlyList<PlatformUserDto>> ListOperatorsAsync(CancellationToken ct = default);
}

/// <summary>Lo mínimo para autenticar y resolver el tenant/rol de un humano.</summary>
public sealed record PlatformUserLookup(
    Guid Id,
    Guid? TenantId,
    string Role,
    string Email,
    string? AuthUserId,
    DateTimeOffset? RevokedAt);
