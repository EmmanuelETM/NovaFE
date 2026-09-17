namespace NovaFE.Application.Tenants.SetEmitterProfile;

/// <summary>
/// Crea o reemplaza (semántica de <c>PUT</c>) el perfil fiscal del emisor del
/// tenant actual (<c>ICurrentTenant</c>) — el operador lo fija con
/// <c>CurrentTenant.Set</c> antes de llamar; self-service ya lo trae resuelto.
/// </summary>
public sealed record SetEmitterProfileCommand(
    string Address,
    string? Municipality,
    string? Province,
    IReadOnlyList<string>? Phones,
    string? Email,
    string? EconomicActivity,
    string DefaultEnvironment);
