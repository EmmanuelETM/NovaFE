using Asp.Versioning;
using NovaFE.Application.Finance.Contracts;
using NovaFE.Application.Finance.GetFiscalSummary;
using NovaFE.Service.Common;
using NovaFE.Service.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NovaFE.Service.Controllers;

/// <summary>
/// Resumen fiscal del tenant actual (lado ventas) — visibilidad interna, no un
/// formato de envío de la DGII. Recurso <b>por contribuyente</b>.
/// </summary>
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/[controller]")]
public sealed class FinanceController(GetFiscalSummaryUseCase getSummary) : ApiController
{
    /// <summary>Resumen fiscal entre <paramref name="from"/> y <paramref name="to"/> (inclusive), por fecha de emisión.</summary>
    [HttpGet("summary")]
    [Authorize(Policy = SecurityPolicies.EcfRead)]
    [ProducesResponseType(typeof(FiscalSummaryDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Summary([FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken ct)
        => (await getSummary.Execute(new GetFiscalSummaryQuery(from, to), ct)).Match(Ok, Problem);
}
