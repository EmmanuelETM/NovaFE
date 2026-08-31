namespace NovaFE.Application.Tenants.SetEmitterProfile;

/// <summary>
/// Crea o reemplaza (semántica de <c>PUT</c>) el perfil fiscal del emisor de un
/// contribuyente. Es un recurso de operador: <see cref="TenantId"/> viene de la ruta.
/// </summary>
public sealed record SetEmitterProfileCommand(
    Guid TenantId,
    string Address,
    string? Municipality,
    string? Province,
    IReadOnlyList<string>? Phones,
    string? Email,
    string? EconomicActivity,
    string DefaultEnvironment);
