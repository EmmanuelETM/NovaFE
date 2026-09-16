using Asp.Versioning;
using NovaFE.Application.Dgii.Contracts;
using NovaFE.Application.Ecf.Contracts;
using NovaFE.Application.Ecf.GetEcf;
using NovaFE.Application.Ecf.GetEcfTrackIds;
using NovaFE.Application.Ecf.IssueEcf;
using NovaFE.Application.Ecf.ListEcf;
using NovaFE.Application.Ecf.Representation;
using NovaFE.Application.Ecf.RetrySubmission;
using NovaFE.Application.Ecf.ValidateEcf;
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
public sealed class EcfController(
    IssueEcfUseCase issue,
    GetEcfUseCase get,
    GetEcfXmlUseCase getXml,
    GetEcfRepresentationUseCase getRepresentation,
    ListEcfUseCase list,
    RetryEcfSubmissionUseCase retry,
    GetEcfTrackIdsUseCase getTrackIds,
    ValidateEcfUseCase validate) : ApiController
{
    /// <summary>
    /// Emite un e-CF. Header opcional <c>Idempotency-Key</c> para reintento seguro.
    /// Devuelve <c>201</c> con el comprobante; <c>200</c> si la clave o el
    /// <c>internalNumber</c> ya se habían usado.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = SecurityPolicies.EcfIssue)]
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

    /// <summary>
    /// Valida un payload sin emitirlo: corre la misma matriz de validación
    /// estructural y fiscal por tipo (Módulo 2 + 6) que <c>POST /ecf</c>, pero no
    /// asigna una secuencia real, no firma y no persiste nada. Un payload que no
    /// cumple devuelve el mismo error que devolvería la emisión real (<c>400</c>
    /// u otro código de negocio) — no hay un cuerpo <c>{ valid: false }</c>; el
    /// código de estado ya lo dice. <c>200</c> trae una vista previa de los
    /// totales calculados y un e-NCF de muestra (nunca uno real).
    /// </summary>
    [HttpPost("validate")]
    [Authorize(Policy = SecurityPolicies.EcfIssue)]
    [ProducesResponseType(typeof(ValidateEcfResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Validate([FromBody] IssueEcfCommand command, CancellationToken ct)
        => (await validate.Execute(command, ct)).Match(Ok, Problem);

    /// <summary>El comprobante emitido y su estado.</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = SecurityPolicies.EcfRead)]
    [ProducesResponseType(typeof(EcfDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => (await get.Execute(new GetEcfQuery(id), ct)).Match(Ok, Problem);

    /// <summary>El XML firmado. <c>?rfce=true</c> devuelve el <c>&lt;RFCE&gt;</c> (tipo 32 &lt; DOP 250 k).</summary>
    [HttpGet("{id:guid}/xml")]
    [Authorize(Policy = SecurityPolicies.EcfRead)]
    public async Task<IActionResult> GetXml(Guid id, [FromQuery] bool rfce, CancellationToken ct)
        => (await getXml.Execute(new GetEcfXmlQuery(id, rfce), ct))
            .Match(xml => Content(xml, "application/xml; charset=utf-8"), Problem);

    /// <summary>
    /// La Representación Impresa en PDF. <c>?layout=letter</c> o <c>pos</c>; si se
    /// omite, rige el formato por defecto del contribuyente
    /// (<c>representation.default_layout</c>, que cae en Carta). <c>?download=true</c>
    /// la descarga en vez de abrirla en el navegador.
    /// </summary>
    [HttpGet("{id:guid}/representation")]
    [Authorize(Policy = SecurityPolicies.EcfRead)]
    [Produces("application/pdf")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRepresentation(
        Guid id,
        [FromQuery] RepresentationLayout? layout,
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
    [Authorize(Policy = SecurityPolicies.EcfRead)]
    [ProducesResponseType(typeof(PagedResult<EcfSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] ListEcfQuery query, CancellationToken ct)
        => (await list.Execute(query, ct)).Match(Ok, Problem);

    /// <summary>
    /// Reencola el envío a la DGII de un comprobante en estado <c>failed</c> o
    /// <c>review</c>. Devuelve <c>202</c> con el comprobante de vuelta en
    /// <c>signed</c>; el worker retoma el envío.
    /// </summary>
    [HttpPost("{id:guid}/retry")]
    [Authorize(Policy = SecurityPolicies.EcfIssue)]
    [ProducesResponseType(typeof(EcfDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Retry(Guid id, CancellationToken ct)
        => (await retry.Execute(new RetryEcfSubmissionCommand(id), ct))
            .Match(dto => Accepted(Url.Action(nameof(GetById), new { id, version = "1" }), dto), Problem);

    /// <summary>
    /// Los trackIds que la DGII tiene registrados para este comprobante (Módulo
    /// 10) — puede haber más de uno si se remitió varias veces. No disponible en
    /// ambiente CerteCF.
    /// </summary>
    [HttpGet("{id:guid}/trackids")]
    [Authorize(Policy = SecurityPolicies.EcfRead)]
    [ProducesResponseType(typeof(IReadOnlyList<DgiiTrackIdEntry>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTrackIds(Guid id, CancellationToken ct)
        => (await getTrackIds.Execute(new GetEcfTrackIdsQuery(id), ct)).Match(Ok, Problem);
}
