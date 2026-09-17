using Asp.Versioning;
using NovaFE.Application.Webhooks.Contracts;
using NovaFE.Application.Webhooks.CreateEndpoint;
using NovaFE.Application.Webhooks.DeleteEndpoint;
using NovaFE.Application.Webhooks.GetEndpoint;
using NovaFE.Application.Webhooks.ListDeliveries;
using NovaFE.Application.Webhooks.ListEndpoints;
using NovaFE.Application.Webhooks.PingEndpoint;
using NovaFE.Application.Webhooks.RetryDelivery;
using NovaFE.Application.Webhooks.RotateEndpointSecret;
using NovaFE.Application.Webhooks.UpdateEndpoint;
using NovaFE.Domain.Common;
using NovaFE.Service.Common;
using NovaFE.Service.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NovaFE.Service.Controllers;

/// <summary>
/// Endpoints de webhook del contribuyente (RF-12.7). Configuración (crear,
/// editar, rotar secreto, borrar) es <b>self-service</b> (política
/// <c>TenantConfig</c>) — el operador no gestiona webhooks a nombre del
/// cliente. El operador conserva las rutas <c>...ForTenant</c> de solo
/// lectura/reintento para soporte, mismo patrón dual que
/// <see cref="ApiKeysController"/>. El <c>secret</c> se devuelve solo al
/// crear el endpoint y al rotarlo. Ver <c>docs/webhooks.md</c>.
/// </summary>
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/[controller]")]
public sealed class WebhooksController(
    CreateWebhookEndpointUseCase create,
    ListWebhookEndpointsUseCase list,
    GetWebhookEndpointUseCase get,
    UpdateWebhookEndpointUseCase update,
    RotateWebhookEndpointSecretUseCase rotateSecret,
    PingWebhookEndpointUseCase ping,
    ListWebhookDeliveriesUseCase listDeliveries,
    RetryWebhookDeliveryUseCase retryDelivery,
    DeleteWebhookEndpointUseCase delete,
    CurrentTenant currentTenant) : ApiController
{
    /// <summary>Registra un endpoint. La respuesta lleva el <c>secret</c> — única vez que se ve.</summary>
    [HttpPost]
    [Authorize(Policy = SecurityPolicies.TenantConfig)]
    [ProducesResponseType(typeof(WebhookEndpointCreatedDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateWebhookEndpointRequest body, CancellationToken ct)
        => (await create.Execute(
                new CreateWebhookEndpointCommand(body.Url, body.Events, body.Description), ct))
            .Match(
                result => CreatedAtAction(nameof(GetById), new { id = result.Endpoint.Id, version = "1" }, result),
                Problem);

    [HttpGet]
    [Authorize(Policy = SecurityPolicies.TenantConfig)]
    [ProducesResponseType(typeof(IReadOnlyList<WebhookEndpointDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct)
        => (await list.Execute(ct)).Match(Ok, Problem);

    [HttpGet("{id:guid}")]
    [Authorize(Policy = SecurityPolicies.TenantConfig)]
    [ProducesResponseType(typeof(WebhookEndpointDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => (await get.Execute(new GetWebhookEndpointQuery(id), ct)).Match(Ok, Problem);

    /// <summary>Actualización parcial: un campo omitido no cambia; <c>description: ""</c> la borra.</summary>
    [HttpPatch("{id:guid}")]
    [Authorize(Policy = SecurityPolicies.TenantConfig)]
    [ProducesResponseType(typeof(WebhookEndpointDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateWebhookEndpointRequest body, CancellationToken ct)
        => (await update.Execute(
                new UpdateWebhookEndpointCommand(id, body.Url, body.Events, body.Enabled, body.Description), ct))
            .Match(Ok, Problem);

    [HttpPost("{id:guid}/rotate-secret")]
    [Authorize(Policy = SecurityPolicies.TenantConfig)]
    [ProducesResponseType(typeof(WebhookSecretDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RotateSecret(Guid id, CancellationToken ct)
        => (await rotateSecret.Execute(new RotateWebhookEndpointSecretCommand(id), ct)).Match(Ok, Problem);

    /// <summary>Envía un <c>webhook.ping</c> al endpoint y devuelve el resultado de la entrega.</summary>
    [HttpPost("{id:guid}/ping")]
    [Authorize(Policy = SecurityPolicies.TenantConfig)]
    [ProducesResponseType(typeof(WebhookPingResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Ping(Guid id, CancellationToken ct)
        => (await ping.Execute(new PingWebhookEndpointCommand(id), ct)).Match(Ok, Problem);

    /// <summary>Log de entregas del endpoint, paginado (más reciente primero).</summary>
    [HttpGet("{id:guid}/deliveries")]
    [Authorize(Policy = SecurityPolicies.TenantConfig)]
    [ProducesResponseType(typeof(PagedResult<WebhookDeliveryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deliveries(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => (await listDeliveries.Execute(new ListWebhookDeliveriesQuery(id, page, pageSize), ct)).Match(Ok, Problem);

    /// <summary>
    /// Reintenta manualmente una entrega <c>dead</c> — vuelve a <c>pending</c>,
    /// lista de inmediato, con intentos y backoff en cero. La escalera
    /// automática solo reanuda si vuelve a fallar.
    /// </summary>
    [HttpPost("{id:guid}/deliveries/{deliveryid:guid}/retry")]
    [Authorize(Policy = SecurityPolicies.TenantConfig)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RetryDelivery(
        Guid id,
        [FromRoute(Name = "deliveryid")] Guid deliveryId,
        CancellationToken ct)
        => (await retryDelivery.Execute(new RetryWebhookDeliveryCommand(currentTenant.Require(), deliveryId), ct))
            .Match(_ => NoContent(), Problem);

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = SecurityPolicies.TenantConfig)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => (await delete.Execute(new DeleteWebhookEndpointCommand(id), ct)).Match(_ => NoContent(), Problem);

    /// <summary>
    /// Los endpoints de webhook de un contribuyente, como <b>operador</b> — soporte.
    /// Solo lectura/reintento: crear o editar un endpoint a nombre del cliente no
    /// tiene un caso de uso real. Mismo patrón dual que <see cref="ApiKeysController"/>.
    /// </summary>
    [HttpGet("~/api/v{version:apiVersion}/tenants/{tenantid:guid}/webhooks")]
    [Authorize(Policy = SecurityPolicies.Operator)]
    [ProducesResponseType(typeof(IReadOnlyList<WebhookEndpointDto>), StatusCodes.Status200OK)]
    public Task<IActionResult> ListForTenant(Guid tenantid, CancellationToken ct)
    {
        currentTenant.Set(tenantid);
        return List(ct);
    }

    /// <summary>Log de entregas de un endpoint de un contribuyente, como operador. Ver <see cref="ListForTenant"/>.</summary>
    [HttpGet("~/api/v{version:apiVersion}/tenants/{tenantid:guid}/webhooks/{id:guid}/deliveries")]
    [Authorize(Policy = SecurityPolicies.Operator)]
    [ProducesResponseType(typeof(PagedResult<WebhookDeliveryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public Task<IActionResult> DeliveriesForTenant(
        Guid tenantid, Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        currentTenant.Set(tenantid);
        return Deliveries(id, page, pageSize, ct);
    }

    /// <summary>Reintenta una entrega muerta de un contribuyente, como operador. Ver <see cref="ListForTenant"/>.</summary>
    [HttpPost("~/api/v{version:apiVersion}/tenants/{tenantid:guid}/webhooks/{id:guid}/deliveries/{deliveryid:guid}/retry")]
    [Authorize(Policy = SecurityPolicies.Operator)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public Task<IActionResult> RetryDeliveryForTenant(
        Guid tenantid,
        Guid id,
        [FromRoute(Name = "deliveryid")] Guid deliveryId,
        CancellationToken ct)
    {
        currentTenant.Set(tenantid);
        return RetryDelivery(id, deliveryId, ct);
    }

    public sealed record CreateWebhookEndpointRequest(
        string? Url,
        IReadOnlyList<string>? Events,
        string? Description);

    public sealed record UpdateWebhookEndpointRequest(
        string? Url,
        IReadOnlyList<string>? Events,
        bool? Enabled,
        string? Description);
}
