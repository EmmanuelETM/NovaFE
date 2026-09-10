using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NovaFE.Application.Users.Contracts;
using NovaFE.Application.Users.GetCurrentUser;
using NovaFE.Service.Common;
using NovaFE.Service.Security;

namespace NovaFE.Service.Controllers;

/// <summary>
/// El usuario que hizo la petición. Lo consume el dashboard para pintar la
/// navegación por rol. Cualquier esquema autenticado sirve: la respuesta cambia
/// según quién sea.
/// </summary>
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/[controller]")]
public sealed class UsersController(GetCurrentUserUseCase getCurrentUser) : ApiController
{
    [HttpGet("me")]
    [Authorize(Policy = SecurityPolicies.Authenticated)]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Me(CancellationToken ct)
        => (await getCurrentUser.Execute(ct)).Match(Ok, Problem);
}
