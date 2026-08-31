using Asp.Versioning;
using NovaFE.Application.Ecf.Contracts;
using NovaFE.Application.Ecf.GetEcf;
using NovaFE.Application.Ecf.IssueEcf;
using NovaFE.Application.Ecf.ListEcf;
using NovaFE.Service.Common;
using Microsoft.AspNetCore.Mvc;

namespace NovaFE.Service.Controllers;

/// <summary>
/// Emisión y consulta de comprobantes fiscales electrónicos. Recurso <b>por
/// tenant</b>: la petición debe traer el header <c>X-Tenant-Id</c>.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public sealed class EcfController(
    IssueEcfUseCase issue,
    GetEcfUseCase get,
    GetEcfXmlUseCase getXml,
    ListEcfUseCase list) : ApiController
{
    /// <summary>
    /// Emite un e-CF. Header opcional <c>Idempotency-Key</c> para reintento seguro.
    /// Devuelve <c>201</c> con el comprobante; <c>200</c> si la clave o el
    /// <c>internalNumber</c> ya se habían usado.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Issue(
        [FromBody] IssueEcfCommand command,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken ct)
    {
        var result = await issue.Execute(command with { IdempotencyKey = idempotencyKey }, ct);

        return result.Match<IActionResult>(
            issued => issued.WasCreated
                ? CreatedAtAction(nameof(GetById), new { id = issued.Ecf.Id, version = "1.0" }, issued.Ecf)
                : Ok(issued.Ecf),
            Problem);
    }

    /// <summary>El comprobante emitido y su estado.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => (await get.Execute(new GetEcfQuery(id), ct)).Match(Ok, Problem);

    /// <summary>El XML firmado. <c>?rfce=true</c> devuelve el <c>&lt;RFCE&gt;</c> (tipo 32 &lt; DOP 250 k).</summary>
    [HttpGet("{id:guid}/xml")]
    public async Task<IActionResult> GetXml(Guid id, [FromQuery] bool rfce, CancellationToken ct)
        => (await getXml.Execute(new GetEcfXmlQuery(id, rfce), ct))
            .Match(xml => Content(xml, "application/xml; charset=utf-8"), Problem);

    /// <summary>Listado paginado de comprobantes emitidos.</summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] ListEcfQuery query, CancellationToken ct)
        => (await list.Execute(query, ct)).Match(Ok, Problem);
}
