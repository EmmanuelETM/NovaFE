namespace NovaFE.Application.Users.Contracts;

/// <summary>
/// Quién es el que hizo la petición, para el dashboard: de acá sale la navegación
/// por rol. Lo devuelve <c>GET /api/v1/users/me</c>.
/// </summary>
/// <param name="TenantId">El contribuyente; <c>null</c> = operador del SaaS.</param>
/// <param name="TenantName">Razón social del contribuyente, si aplica.</param>
public sealed record UserProfileDto(
    string Id,
    string? Email,
    string Role,
    Guid? TenantId,
    string? TenantName);
