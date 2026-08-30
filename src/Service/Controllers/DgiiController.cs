using Asp.Versioning;
using NovaFE.Application.Dgii.CheckDgiiConnection;
using NovaFE.Service.Common;
using Microsoft.AspNetCore.Mvc;

namespace NovaFE.Service.Controllers;

/// <summary>
/// Utilidades de integración con la DGII. Recurso <b>por tenant</b> (header
/// <c>X-Tenant-Id</c>).
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
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
