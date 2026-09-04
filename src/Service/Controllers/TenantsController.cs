using Asp.Versioning;
using NovaFE.Application.Tenants.CreateApiKey;
using NovaFE.Application.Tenants.GetEmitterProfile;
using NovaFE.Application.Tenants.GetTenant;
using NovaFE.Application.Tenants.ListApiKeys;
using NovaFE.Application.Tenants.ListTenants;
using NovaFE.Application.Tenants.RegisterTenant;
using NovaFE.Application.Tenants.RevokeApiKey;
using NovaFE.Application.Tenants.SetEmitterProfile;
using NovaFE.Service.Common;
using NovaFE.Service.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NovaFE.Service.Controllers;

/// <summary>
/// Alta y consulta de contribuyentes, su perfil fiscal de emisor y sus API keys.
/// Es un recurso de <b>operador</b> del SaaS: se autentica con la clave de
/// operador (header <c>X-Admin-Key</c>), no con un tenant.
/// </summary>
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize(Policy = SecurityPolicies.Operator)]
public sealed class TenantsController(
    RegisterTenantUseCase register,
    GetTenantUseCase get,
    ListTenantsUseCase list,
    GetEmitterProfileUseCase getEmitterProfile,
    SetEmitterProfileUseCase setEmitterProfile,
    CreateApiKeyUseCase createApiKey,
    ListApiKeysUseCase listApiKeys,
    RevokeApiKeyUseCase revokeApiKey) : ApiController
{
    [HttpPost]
    public async Task<IActionResult> Register(
        [FromBody] RegisterTenantCommand command,
        CancellationToken ct)
        => (await register.Execute(command, ct)).Match(
            id => CreatedAtAction(nameof(GetById), new { id, version = "1" }, new { id }),
            Problem);

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => (await get.Execute(new GetTenantQuery(id), ct)).Match(Ok, Problem);

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] ListTenantsQuery query,
        CancellationToken ct)
        => (await list.Execute(query, ct)).Match(Ok, Problem);

    /// <summary>El perfil fiscal del emisor (dirección, ubicación, teléfonos, ambiente).</summary>
    [HttpGet("{id:guid}/emitter-profile")]
    public async Task<IActionResult> GetEmitterProfile(Guid id, CancellationToken ct)
        => (await getEmitterProfile.Execute(new GetEmitterProfileQuery(id), ct)).Match(Ok, Problem);

    /// <summary>Crea o reemplaza el perfil fiscal del emisor (upsert).</summary>
    [HttpPut("{id:guid}/emitter-profile")]
    public async Task<IActionResult> SetEmitterProfile(
        Guid id,
        [FromBody] SetEmitterProfileBody body,
        CancellationToken ct)
        => (await setEmitterProfile.Execute(
                new SetEmitterProfileCommand(
                    id,
                    body.Address,
                    body.Municipality,
                    body.Province,
                    body.Phones,
                    body.Email,
                    body.EconomicActivity,
                    body.DefaultEnvironment),
                ct))
            .Match(Ok, Problem);

    /// <summary>
    /// Acuña una API key para el contribuyente. El <c>token</c> de la respuesta es
    /// la <b>única</b> vez que se puede ver: guárdalo.
    /// </summary>
    [HttpPost("{id:guid}/api-keys")]
    public async Task<IActionResult> CreateApiKey(
        Guid id,
        [FromBody] CreateApiKeyBody? body,
        CancellationToken ct)
        => (await createApiKey.Execute(
                new CreateApiKeyCommand(id, body?.Label, body?.Environment, body?.ExpiresAt), ct))
            .Match(
                created => CreatedAtAction(nameof(ListApiKeys), new { id, version = "1" }, created),
                Problem);

    /// <summary>Las API keys del contribuyente (sin los tokens).</summary>
    [HttpGet("{id:guid}/api-keys")]
    public async Task<IActionResult> ListApiKeys(Guid id, CancellationToken ct)
        => (await listApiKeys.Execute(new ListApiKeysQuery(id), ct)).Match(Ok, Problem);

    /// <summary>Revoca una API key. Deja de autenticar de inmediato.</summary>
    [HttpDelete("{id:guid}/api-keys/{keyid:guid}")]
    public async Task<IActionResult> RevokeApiKey(
        Guid id,
        [FromRoute(Name = "keyid")] Guid keyId,
        CancellationToken ct)
        => (await revokeApiKey.Execute(new RevokeApiKeyCommand(id, keyId), ct))
            .Match(_ => NoContent(), Problem);

    /// <summary>Cuerpo del <c>PUT .../emitter-profile</c> (el contribuyente va en la ruta).</summary>
    public sealed record SetEmitterProfileBody(
        string Address,
        string? Municipality,
        string? Province,
        IReadOnlyList<string>? Phones,
        string? Email,
        string? EconomicActivity,
        string DefaultEnvironment);

    /// <summary>
    /// Cuerpo del <c>POST .../api-keys</c>; todo opcional. <c>environment</c> por
    /// defecto es el del perfil de emisor.
    /// </summary>
    public sealed record CreateApiKeyBody(string? Label, string? Environment, DateTimeOffset? ExpiresAt);
}
