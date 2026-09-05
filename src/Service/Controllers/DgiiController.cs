using Asp.Versioning;
using NovaFE.Application.Dgii.CheckDgiiConnection;
using NovaFE.Service.Common;
using NovaFE.Service.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NovaFE.Service.Controllers;

/// <summary>
/// Utilidades de integración con la DGII. Recurso <b>por contribuyente</b>: la
/// petición se autentica con una API key (header <c>X-API-Key</c>).
/// </summary>
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize(Policy = SecurityPolicies.TenantConfig)]
public sealed class DgiiController(CheckDgiiConnectionUseCase checkConnection) : ApiController
{
    /// <summary>
    /// Comprueba que el contribuyente puede autenticarse ante la DGII en el
    /// ambiente indicado. Fuerza el flujo semilla → token si hace falta. No
    /// devuelve el token.
    /// </summary>
    [HttpGet("connection")]
    public async Task<IActionResult> Connection([FromQuery] string environment, CancellationToken ct)
        => (await checkConnection.Execute(new CheckDgiiConnectionQuery(environment), ct)).Match(Ok, Problem);
}
