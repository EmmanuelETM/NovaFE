using Asp.Versioning;
using NovaFE.Application.Sequences.AllocateNcf;
using NovaFE.Application.Sequences.Contracts;
using NovaFE.Application.Sequences.GetSequence;
using NovaFE.Application.Sequences.ListSequences;
using NovaFE.Application.Sequences.RegisterSequenceRange;
using NovaFE.Service.Common;
using NovaFE.Service.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NovaFE.Service.Controllers;

/// <summary>
/// Inventario de secuencias e-NCF del contribuyente. Recurso <b>por
/// contribuyente</b>: la petición se autentica con una API key (header
/// <c>X-API-Key</c>) — salvo las acciones <c>...ForTenant</c>, de operador. Ver
/// el comentario de <c>CertificatesController.UploadForTenant</c> para el
/// porqué (candado de onboarding) y la nota sobre RLS.
/// </summary>
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/[controller]")]
public sealed class SequencesController(
    RegisterSequenceRangeUseCase register,
    GetSequenceUseCase get,
    ListSequencesUseCase list,
    AllocateNcfUseCase allocate,
    CurrentTenant currentTenant) : ApiController
{
    /// <summary>Registra un rango de e-NCF autorizado por la DGII.</summary>
    [HttpPost]
    [Authorize(Policy = SecurityPolicies.TenantConfig)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterSequenceRangeCommand command,
        CancellationToken ct)
        => (await register.Execute(command, ct)).Match(
            id => CreatedAtAction(nameof(GetById), new { id, version = "1" }, new { id }),
            Problem);

    [HttpGet("{id:guid}")]
    [Authorize(Policy = SecurityPolicies.TenantConfig)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => (await get.Execute(new GetSequenceQuery(id), ct)).Match(Ok, Problem);

    [HttpGet]
    [Authorize(Policy = SecurityPolicies.TenantConfig)]
    public async Task<IActionResult> List(CancellationToken ct)
        => (await list.Execute(ct)).Match(Ok, Problem);

    /// <summary>Toma la siguiente secuencia disponible para un tipo y ambiente.</summary>
    [HttpPost("allocate")]
    [Authorize(Policy = SecurityPolicies.TenantConfig)]
    public async Task<IActionResult> Allocate(
        [FromBody] AllocateNcfCommand command,
        CancellationToken ct)
        => (await allocate.Execute(command, ct)).Match(Ok, Problem);

    /// <summary>
    /// Registra un rango de e-NCF para un contribuyente, como <b>operador</b>. Ver
    /// <c>CertificatesController.UploadForTenant</c> para el porqué (candado de
    /// onboarding) y la nota sobre RLS.
    /// </summary>
    [HttpPost("~/api/v{version:apiVersion}/tenants/{tenantid:guid}/sequences")]
    [Authorize(Policy = SecurityPolicies.Operator)]
    public Task<IActionResult> RegisterForTenant(
        Guid tenantid, [FromBody] RegisterSequenceRangeCommand command, CancellationToken ct)
    {
        currentTenant.Set(tenantid);
        return Register(command, ct);
    }

    /// <summary>Las secuencias de un contribuyente, como operador. Ver <see cref="RegisterForTenant"/>.</summary>
    [HttpGet("~/api/v{version:apiVersion}/tenants/{tenantid:guid}/sequences")]
    [Authorize(Policy = SecurityPolicies.Operator)]
    [ProducesResponseType(typeof(IReadOnlyList<NcfSequenceDto>), StatusCodes.Status200OK)]
    public Task<IActionResult> ListForTenant(Guid tenantid, CancellationToken ct)
    {
        currentTenant.Set(tenantid);
        return List(ct);
    }
}
