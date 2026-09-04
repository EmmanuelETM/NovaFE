using NovaFE.Application.Tenants.Contracts;

namespace NovaFE.Application.Tenants.Interfaces;

/// <summary>Read side (Dapper) de las API keys.</summary>
public interface IApiKeyReadRepository
{
    /// <summary>Las credenciales de un contribuyente, la más reciente primero.</summary>
    Task<IReadOnlyList<ApiKeyDto>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// Resuelve una credencial por el hash de su token. La usa el autenticador y
    /// por eso cruza tenants (todavía no hay tenant en la petición).
    /// </summary>
    Task<ApiKeyLookup?> FindByHashAsync(string keyHash, CancellationToken ct = default);
}

/// <summary>Lo mínimo para autenticar: identidad + estado de vigencia.</summary>
public sealed record ApiKeyLookup(
    Guid Id,
    Guid TenantId,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? RevokedAt);
