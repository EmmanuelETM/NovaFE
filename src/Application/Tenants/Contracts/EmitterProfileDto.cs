namespace NovaFE.Application.Tenants.Contracts;

/// <summary>Vista del perfil fiscal del emisor.</summary>
public sealed record EmitterProfileDto(
    Guid Id,
    Guid TenantId,
    string Address,
    string? Municipality,
    string? Province,
    string[] Phones,
    string? Email,
    string? EconomicActivity,
    string DefaultEnvironment,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
