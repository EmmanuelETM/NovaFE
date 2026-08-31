using Asp.Versioning;
using NovaFE.Application.Tenants.GetEmitterProfile;
using NovaFE.Application.Tenants.GetTenant;
using NovaFE.Application.Tenants.ListTenants;
using NovaFE.Application.Tenants.RegisterTenant;
using NovaFE.Application.Tenants.SetEmitterProfile;
using NovaFE.Service.Common;
using Microsoft.AspNetCore.Mvc;

namespace NovaFE.Service.Controllers;

/// <summary>
/// Alta y consulta de contribuyentes, y su perfil fiscal de emisor. Es un recurso
/// de operador del SaaS: no requiere un tenant en la petición (un contribuyente se
/// registra antes de existir).
/// </summary>
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/[controller]")]
public sealed class TenantsController(
    RegisterTenantUseCase register,
    GetTenantUseCase get,
    ListTenantsUseCase list,
    GetEmitterProfileUseCase getEmitterProfile,
    SetEmitterProfileUseCase setEmitterProfile) : ApiController
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

    /// <summary>Cuerpo del <c>PUT .../emitter-profile</c> (el contribuyente va en la ruta).</summary>
    public sealed record SetEmitterProfileBody(
        string Address,
        string? Municipality,
        string? Province,
        IReadOnlyList<string>? Phones,
        string? Email,
        string? EconomicActivity,
        string DefaultEnvironment);
}
