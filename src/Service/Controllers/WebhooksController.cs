using Asp.Versioning;
using NovaFE.Application.Webhooks.CreateEndpoint;
using NovaFE.Application.Webhooks.DeleteEndpoint;
using NovaFE.Application.Webhooks.GetEndpoint;
using NovaFE.Application.Webhooks.ListDeliveries;
using NovaFE.Application.Webhooks.ListEndpoints;
using NovaFE.Application.Webhooks.PingEndpoint;
using NovaFE.Application.Webhooks.RotateEndpointSecret;
using NovaFE.Application.Webhooks.UpdateEndpoint;
using NovaFE.Service.Common;
using NovaFE.Service.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NovaFE.Service.Controllers;

/// <summary>
/// Endpoints de webhook del contribuyente (RF-12.7). Recurso <b>por contribuyente</b>:
/// la petición se autentica con una API key de rol <c>admin_tenant</c>. El
/// <c>secret</c> se devuelve solo al crear el endpoint y al rotarlo. Ver
/// <c>docs/webhooks.md</c>.
/// </summary>
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize(Policy = SecurityPolicies.TenantConfig)]
public sealed class WebhooksController(
    CreateWebhookEndpointUseCase create,
    ListWebhookEndpointsUseCase list,
    GetWebhookEndpointUseCase get,
    UpdateWebhookEndpointUseCase update,
    RotateWebhookEndpointSecretUseCase rotateSecret,
    PingWebhookEndpointUseCase ping,
    ListWebhookDeliveriesUseCase listDeliveries,
    DeleteWebhookEndpointUseCase delete) : ApiController
{
    /// <summary>Registra un endpoint. La respuesta lleva el <c>secret</c> — única vez que se ve.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateWebhookEndpointRequest body, CancellationToken ct)
        => (await create.Execute(
                new CreateWebhookEndpointCommand(body.Url, body.Events, body.Description), ct))
            .Match(
                result => CreatedAtAction(nameof(GetById), new { id = result.Endpoint.Id, version = "1" }, result),
                Problem);

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => (await list.Execute(ct)).Match(Ok, Problem);

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => (await get.Execute(new GetWebhookEndpointQuery(id), ct)).Match(Ok, Problem);

    /// <summary>Actualización parcial: un campo omitido no cambia; <c>description: ""</c> la borra.</summary>
    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateWebhookEndpointRequest body, CancellationToken ct)
        => (await update.Execute(
                new UpdateWebhookEndpointCommand(id, body.Url, body.Events, body.Enabled, body.Description), ct))
            .Match(Ok, Problem);

    [HttpPost("{id:guid}/rotate-secret")]
    public async Task<IActionResult> RotateSecret(Guid id, CancellationToken ct)
        => (await rotateSecret.Execute(new RotateWebhookEndpointSecretCommand(id), ct)).Match(Ok, Problem);

    /// <summary>Envía un <c>webhook.ping</c> al endpoint y devuelve el resultado de la entrega.</summary>
    [HttpPost("{id:guid}/ping")]
    public async Task<IActionResult> Ping(Guid id, CancellationToken ct)
        => (await ping.Execute(new PingWebhookEndpointCommand(id), ct)).Match(Ok, Problem);

    /// <summary>Log de entregas del endpoint, paginado (más reciente primero).</summary>
    [HttpGet("{id:guid}/deliveries")]
    public async Task<IActionResult> Deliveries(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => (await listDeliveries.Execute(new ListWebhookDeliveriesQuery(id, page, pageSize), ct)).Match(Ok, Problem);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => (await delete.Execute(new DeleteWebhookEndpointCommand(id), ct)).Match(_ => NoContent(), Problem);

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
