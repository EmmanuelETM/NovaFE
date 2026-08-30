using Asp.Versioning;
using NovaFE.Application.Sequences.AllocateNcf;
using NovaFE.Application.Sequences.GetSequence;
using NovaFE.Application.Sequences.ListSequences;
using NovaFE.Application.Sequences.RegisterSequenceRange;
using NovaFE.Service.Common;
using Microsoft.AspNetCore.Mvc;

namespace NovaFE.Service.Controllers;

/// <summary>
/// Inventario de secuencias e-NCF del contribuyente. Recurso <b>por tenant</b>: la
/// petición debe traer el header <c>X-Tenant-Id</c>.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public sealed class SequencesController(
    RegisterSequenceRangeUseCase register,
    GetSequenceUseCase get,
    ListSequencesUseCase list,
    AllocateNcfUseCase allocate) : ApiController
{
    /// <summary>Registra un rango de e-NCF autorizado por la DGII.</summary>
    [HttpPost]
    public async Task<IActionResult> Register(
        [FromBody] RegisterSequenceRangeCommand command,
        CancellationToken ct)
        => (await register.Execute(command, ct)).Match(
            id => CreatedAtAction(nameof(GetById), new { id, version = "1.0" }, new { id }),
            Problem);

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => (await get.Execute(new GetSequenceQuery(id), ct)).Match(Ok, Problem);

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => (await list.Execute(ct)).Match(Ok, Problem);

    /// <summary>Toma la siguiente secuencia disponible para un tipo y ambiente.</summary>
    [HttpPost("allocate")]
    public async Task<IActionResult> Allocate(
        [FromBody] AllocateNcfCommand command,
        CancellationToken ct)
        => (await allocate.Execute(command, ct)).Match(Ok, Problem);
}
