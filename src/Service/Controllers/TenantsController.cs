using Asp.Versioning;
using NovaFE.Application.Tenants.GetTenant;
using NovaFE.Application.Tenants.ListTenants;
using NovaFE.Application.Tenants.RegisterTenant;
using NovaFE.Service.Common;
using Microsoft.AspNetCore.Mvc;

namespace NovaFE.Service.Controllers;

/// <summary>
/// Alta y consulta de contribuyentes. Es un recurso de operador del SaaS: no
/// requiere un tenant en la petición (un contribuyente se registra antes de
/// existir).
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public sealed class TenantsController(
    RegisterTenantUseCase register,
    GetTenantUseCase get,
    ListTenantsUseCase list) : ApiController
{
    [HttpPost]
    public async Task<IActionResult> Register(
        [FromBody] RegisterTenantCommand command,
        CancellationToken ct)
        => (await register.Execute(command, ct)).Match(
            id => CreatedAtAction(nameof(GetById), new { id, version = "1.0" }, new { id }),
            Problem);

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => (await get.Execute(new GetTenantQuery(id), ct)).Match(Ok, Problem);

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] ListTenantsQuery query,
        CancellationToken ct)
        => (await list.Execute(query, ct)).Match(Ok, Problem);
}
