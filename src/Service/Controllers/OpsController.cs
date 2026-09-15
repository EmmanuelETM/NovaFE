using Asp.Versioning;
using NovaFE.Application.Ops.Contracts;
using NovaFE.Application.Ops.GetDgiiStatus;
using NovaFE.Application.Ops.GetOpsStatus;
using NovaFE.Service.Common;
using NovaFE.Service.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NovaFE.Service.Controllers;

/// <summary>
/// Estado operacional en vivo de la plataforma (docs/observability.md).
/// Recurso de <b>operador</b> (header <c>X-Admin-Key</c>) — no es información
/// de negocio de ningún tenant.
/// </summary>
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize(Policy = SecurityPolicies.Operator)]
public sealed class OpsController(GetOpsStatusUseCase getStatus, GetDgiiStatusUseCase getDgiiStatus) : ApiController
{
    /// <summary>Latido de los workers, profundidad/antigüedad de los outbox, y salud de las secuencias.</summary>
    [HttpGet("status")]
    [ProducesResponseType(typeof(OpsStatusDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Status(CancellationToken ct)
        => (await getStatus.Execute(ct)).Match(Ok, Problem);

    /// <summary>
    /// Diagnóstico crudo del estatus de servicio de la DGII (Módulo 10,
    /// <c>statusecf.dgii.gov.do</c>). El schema de la respuesta no está
    /// documentado por la DGII — es lectura tolerante para mirar a mano, no una
    /// señal que alimente ninguna decisión automática. Ver docs/dgii-queries.md.
    /// </summary>
    [HttpGet("dgii-status")]
    [ProducesResponseType(typeof(DgiiStatusDiagnosticDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> DgiiStatus(CancellationToken ct)
        => (await getDgiiStatus.Execute(ct)).Match(Ok, Problem);
}
