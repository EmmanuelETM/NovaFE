namespace NovaFE.Application.Tenants.UpdateEmitterProfile;

/// <summary>
/// Actualiza el perfil fiscal del emisor del tenant actual — self-service,
/// <c>admin_tenant</c>. A propósito **no** lleva <c>DefaultEnvironment</c>:
/// pasar de un ambiente a otro exige certificado y rango de secuencia
/// autorizados en el ambiente destino, algo que solo el operador puede
/// confirmar. Ese campo sigue siendo del operador
/// (<c>TenantsController.SetEmitterProfile</c> →
/// <c>SetEmitterProfileCommand</c>). Tampoco crea el perfil si no existe
/// (eso es parte del onboarding, de operador) — solo actualiza uno ya
/// creado.
/// </summary>
public sealed record UpdateEmitterProfileCommand(
    string Address,
    string? Municipality,
    string? Province,
    IReadOnlyList<string>? Phones,
    string? Email,
    string? EconomicActivity);
