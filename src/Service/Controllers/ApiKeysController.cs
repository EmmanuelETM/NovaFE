using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NovaFE.Application.Tenants.Contracts;
using NovaFE.Application.Tenants.CreateApiKey;
using NovaFE.Application.Tenants.ListApiKeys;
using NovaFE.Application.Tenants.RevokeApiKey;
using NovaFE.Service.Common;
using NovaFE.Service.Security;

namespace NovaFE.Service.Controllers;

/// <summary>
/// API keys del contribuyente. <b>Self-service</b> (política <c>TenantConfig</c>,
/// igual que certificados/secuencias/perfil fiscal) — acuñar, listar y revocar
/// una key es una acción del cliente, no del operador (Fase 2, ver
/// <c>docs/multi-tenancy-hierarchy.md</c>). El operador conserva las rutas
/// <c>...ForTenant</c> para soporte, mismo patrón dual que
/// <see cref="CertificatesController"/>.
/// </summary>
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/[controller]")]
public sealed class ApiKeysController(
    CreateApiKeyUseCase create,
    ListApiKeysUseCase list,
    RevokeApiKeyUseCase revoke,
    CurrentTenant currentTenant) : ApiController
{
    /// <summary>
    /// Acuña una API key para el tenant activo. El <c>token</c> de la
    /// respuesta es la <b>única</b> vez que se puede ver: guárdalo.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = SecurityPolicies.TenantConfig)]
    [ProducesResponseType(typeof(ApiKeyCreatedDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateApiKeyBody? body, CancellationToken ct)
        => (await create.Execute(
                new CreateApiKeyCommand(currentTenant.Require(), body?.Label, body?.Environment, body?.Role, body?.ExpiresAt),
                ct))
            .Match(
                created => CreatedAtAction(nameof(List), new { version = "1" }, created),
                Problem);

    /// <summary>Las API keys del tenant activo (sin los tokens).</summary>
    [HttpGet]
    [Authorize(Policy = SecurityPolicies.TenantConfig)]
    [ProducesResponseType(typeof(IReadOnlyList<ApiKeyDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct)
        => (await list.Execute(new ListApiKeysQuery(currentTenant.Require()), ct)).Match(Ok, Problem);

    /// <summary>Revoca una API key del tenant activo. Deja de autenticar de inmediato.</summary>
    [HttpDelete("{keyid:guid}")]
    [Authorize(Policy = SecurityPolicies.TenantConfig)]
    public async Task<IActionResult> Revoke([FromRoute(Name = "keyid")] Guid keyId, CancellationToken ct)
        => (await revoke.Execute(new RevokeApiKeyCommand(currentTenant.Require(), keyId), ct))
            .Match(_ => NoContent(), Problem);

    /// <summary>
    /// Acuña una API key para un contribuyente, como <b>operador</b> — soporte,
    /// o el flujo previo a que el cliente entre al dashboard. Ver el
    /// comentario de <c>CertificatesController.UploadForTenant</c> para la
    /// nota sobre RLS.
    /// </summary>
    [HttpPost("~/api/v{version:apiVersion}/tenants/{tenantid:guid}/api-keys")]
    [Authorize(Policy = SecurityPolicies.Operator)]
    public Task<IActionResult> CreateForTenant(Guid tenantid, [FromBody] CreateApiKeyBody? body, CancellationToken ct)
    {
        currentTenant.Set(tenantid);
        return Create(body, ct);
    }

    /// <summary>Las API keys de un contribuyente, como operador. Ver <see cref="CreateForTenant"/>.</summary>
    [HttpGet("~/api/v{version:apiVersion}/tenants/{tenantid:guid}/api-keys")]
    [Authorize(Policy = SecurityPolicies.Operator)]
    [ProducesResponseType(typeof(IReadOnlyList<ApiKeyDto>), StatusCodes.Status200OK)]
    public Task<IActionResult> ListForTenant(Guid tenantid, CancellationToken ct)
    {
        currentTenant.Set(tenantid);
        return List(ct);
    }

    /// <summary>Revoca la API key de un contribuyente, como operador. Ver <see cref="CreateForTenant"/>.</summary>
    [HttpDelete("~/api/v{version:apiVersion}/tenants/{tenantid:guid}/api-keys/{keyid:guid}")]
    [Authorize(Policy = SecurityPolicies.Operator)]
    public Task<IActionResult> RevokeForTenant(
        Guid tenantid, [FromRoute(Name = "keyid")] Guid keyId, CancellationToken ct)
    {
        currentTenant.Set(tenantid);
        return Revoke(keyId, ct);
    }

    /// <summary>
    /// Cuerpo del <c>POST /api-keys</c>. <c>environment</c> por defecto es el
    /// del perfil de emisor; <c>role</c> (<c>admin_tenant</c> / <c>emisor</c> /
    /// <c>consultor</c>, RF-14.5) es obligatorio — sin default.
    /// </summary>
    public sealed record CreateApiKeyBody(string? Label, string? Environment, string Role, DateTimeOffset? ExpiresAt);
}
