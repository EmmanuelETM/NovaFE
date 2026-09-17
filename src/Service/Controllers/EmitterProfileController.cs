using Asp.Versioning;
using NovaFE.Application.Tenants.Contracts;
using NovaFE.Application.Tenants.GetEmitterProfile;
using NovaFE.Application.Tenants.UpdateEmitterProfile;
using NovaFE.Service.Common;
using NovaFE.Service.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NovaFE.Service.Controllers;

/// <summary>
/// El perfil fiscal del emisor (dirección, ubicación, teléfonos) del tenant
/// actual — self-service, <c>admin_tenant</c>. No incluye el ambiente de la
/// DGII: eso lo cambia solo el operador, porque pasar de un ambiente a otro
/// exige certificado y rango de secuencia ya autorizados ahí (ver
/// <see cref="UpdateEmitterProfileCommand"/>). Esa variante, y la de crear el
/// perfil la primera vez (onboarding), viven en
/// <c>TenantsController.{Get,Set}EmitterProfile</c>.
/// </summary>
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize(Policy = SecurityPolicies.TenantConfig)]
public sealed class EmitterProfileController(
    GetEmitterProfileUseCase get,
    UpdateEmitterProfileUseCase update) : ApiController
{
    [HttpGet]
    [ProducesResponseType(typeof(EmitterProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Get(CancellationToken ct)
        => (await get.Execute(ct)).Match(Ok, Problem);

    /// <summary>Actualiza el perfil fiscal del emisor (no crea uno nuevo).</summary>
    [HttpPut]
    [ProducesResponseType(typeof(EmitterProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update([FromBody] UpdateEmitterProfileCommand command, CancellationToken ct)
        => (await update.Execute(command, ct)).Match(Ok, Problem);
}
