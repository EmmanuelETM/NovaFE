namespace NovaFE.Application.Tenants.CreateApiKey;

/// <summary>
/// Acuña una API key para un contribuyente. Recurso de operador:
/// <see cref="TenantId"/> viene de la ruta.
/// </summary>
/// <param name="Environment">
/// Ambiente de la DGII de la key (<c>Test</c> / <c>Cert</c> / <c>Production</c>).
/// Si viene vacío se usa el <c>DefaultEnvironment</c> del perfil de emisor.
/// </param>
/// <param name="Role">
/// Rol de la key (<c>admin_tenant</c> / <c>emisor</c> / <c>consultor</c>, RF-14.5).
/// Sin default: a diferencia del ambiente, el operador siempre lo declara.
/// </param>
public sealed record CreateApiKeyCommand(
    Guid TenantId,
    string? Label,
    string? Environment,
    string? Role,
    DateTimeOffset? ExpiresAt);
