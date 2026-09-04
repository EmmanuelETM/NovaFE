using Asp.Versioning;
using NovaFE.Application.Ecf.Contracts;
using NovaFE.Application.Ecf.GetEcf;
using NovaFE.Application.Ecf.IssueEcf;
using NovaFE.Application.Ecf.ListEcf;
using NovaFE.Application.Ecf.Representation;
using NovaFE.Application.Ecf.RetrySubmission;
using NovaFE.Domain.Common;
using NovaFE.Service.Common;
using NovaFE.Service.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NovaFE.Service.Controllers;

/// <summary>
/// Emisión y consulta de comprobantes fiscales electrónicos. Recurso <b>por
/// contribuyente</b>: la petición se autentica con una API key (header
/// <c>X-API-Key</c>).
/// </summary>
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize(Policy = SecurityPolicies.TenantClient)]
public sealed class EcfController(
    IssueEcfUseCase issue,
    GetEcfUseCase get,
    GetEcfXmlUseCase getXml,
    GetEcfRepresentationUseCase getRepresentation,
    ListEcfUseCase list,
    RetryEcfSubmissionUseCase retry) : ApiController
{
    /// <summary>
    /// Emite un e-CF. Header opcional <c>Idempotency-Key</c> para reintento seguro.
    /// Devuelve <c>201</c> con el comprobante; <c>200</c> si la clave o el
    /// <c>internalNumber</c> ya se habían usado.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(EcfDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(EcfDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Issue(
        [FromBody] IssueEcfCommand command,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken ct)
    {
        var result = await issue.Execute(command with { IdempotencyKey = idempotencyKey }, ct);

        return result.Match(
            issued => issued.WasCreated
                ? CreatedAtAction(nameof(GetById), new { id = issued.Ecf.Id, version = "1" }, issued.Ecf)
                : Ok(issued.Ecf),
            Problem);
    }

    /// <summary>El comprobante emitido y su estado.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(EcfDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => (await get.Execute(new GetEcfQuery(id), ct)).Match(Ok, Problem);

    /// <summary>El XML firmado. <c>?rfce=true</c> devuelve el <c>&lt;RFCE&gt;</c> (tipo 32 &lt; DOP 250 k).</summary>
    [HttpGet("{id:guid}/xml")]
    public async Task<IActionResult> GetXml(Guid id, [FromQuery] bool rfce, CancellationToken ct)
        => (await getXml.Execute(new GetEcfXmlQuery(id, rfce), ct))
            .Match(xml => Content(xml, "application/xml; charset=utf-8"), Problem);

    /// <summary>
    /// La Representación Impresa en PDF. <c>?layout=letter</c> (por defecto) o
    /// <c>pos</c>; <c>?download=true</c> la descarga en vez de abrirla en el navegador.
    /// </summary>
    [HttpGet("{id:guid}/representation")]
    [Produces("application/pdf")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRepresentation(
        Guid id,
        [FromQuery] RepresentationLayout layout,
        [FromQuery] bool download,
        CancellationToken ct)
        => (await getRepresentation.Execute(new GetEcfRepresentationQuery(id, layout), ct))
            .Match(
                result =>
                {
                    Response.Headers.ContentDisposition =
                        $"{(download ? "attachment" : "inline")}; filename=\"{result.FileName}\"";
                    return File(result.Pdf, "application/pdf");
                },
                Problem);

    /// <summary>Listado paginado de comprobantes emitidos.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<EcfSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] ListEcfQuery query, CancellationToken ct)
        => (await list.Execute(query, ct)).Match(Ok, Problem);

    /// <summary>
    /// Reencola el envío a la DGII de un comprobante en estado <c>failed</c> o
    /// <c>review</c>. Devuelve <c>202</c> con el comprobante de vuelta en
    /// <c>signed</c>; el worker retoma el envío.
    /// </summary>
    [HttpPost("{id:guid}/retry")]
    [ProducesResponseType(typeof(EcfDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Retry(Guid id, CancellationToken ct)
        => (await retry.Execute(new RetryEcfSubmissionCommand(id), ct))
            .Match(dto => Accepted(Url.Action(nameof(GetById), new { id, version = "1" }), dto), Problem);
}
