using Asp.Versioning;
using NovaFE.Application.Tenants.Contracts;
using NovaFE.Application.Tenants.GetEmitterProfile;
using NovaFE.Application.Tenants.SetEmitterProfile;
using NovaFE.Service.Common;
using NovaFE.Service.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NovaFE.Service.Controllers;

/// <summary>
/// El perfil fiscal del emisor (dirección, ubicación, teléfonos, ambiente) del
/// tenant actual — self-service, <c>admin_tenant</c>. La contraparte de
/// operador (por id, para el onboarding antes de que el tenant tenga sesión)
/// es <c>TenantsController.{Get,Set}EmitterProfile</c>; los dos llaman al
/// mismo caso de uso.
/// </summary>
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize(Policy = SecurityPolicies.TenantConfig)]
public sealed class EmitterProfileController(
    GetEmitterProfileUseCase get,
    SetEmitterProfileUseCase set) : ApiController
{
    [HttpGet]
    [ProducesResponseType(typeof(EmitterProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Get(CancellationToken ct)
        => (await get.Execute(ct)).Match(Ok, Problem);

    /// <summary>Crea o reemplaza el perfil fiscal del emisor (upsert).</summary>
    [HttpPut]
    [ProducesResponseType(typeof(EmitterProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Set([FromBody] SetEmitterProfileCommand command, CancellationToken ct)
        => (await set.Execute(command, ct)).Match(Ok, Problem);
}
