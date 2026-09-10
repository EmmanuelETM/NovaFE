using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NovaFE.Application.Users.Contracts;
using NovaFE.Application.Users.ListOperatorUsers;
using NovaFE.Application.Users.ProvisionOperatorUser;
using NovaFE.Application.Users.ReinstateUser;
using NovaFE.Application.Users.RevokeUser;
using NovaFE.Service.Common;
using NovaFE.Service.Security;

namespace NovaFE.Service.Controllers;

/// <summary>
/// Operadores del SaaS en el dashboard (rol <c>admin_sistema</c>, sin
/// contribuyente). Recurso de operador: el primero se aprovisiona con el
/// rompe-cristal <c>X-Admin-Key</c>; los siguientes, con la sesión de un operador.
/// </summary>
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize(Policy = SecurityPolicies.Operator)]
public sealed class OperatorUsersController(
    ProvisionOperatorUserUseCase provision,
    ListOperatorUsersUseCase list,
    RevokeUserUseCase revoke,
    ReinstateUserUseCase reinstate) : ApiController
{
    [HttpPost]
    [ProducesResponseType(typeof(PlatformUserDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Provision([FromBody] ProvisionOperatorBody body, CancellationToken ct)
        => (await provision.Execute(new ProvisionOperatorUserCommand(body.Email), ct))
            .Match(user => CreatedAtAction(nameof(List), new { version = "1" }, user), Problem);

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PlatformUserDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct)
        => (await list.Execute(ct)).Match(Ok, Problem);

    [HttpDelete("{userid:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Revoke([FromRoute(Name = "userid")] Guid userId, CancellationToken ct)
        => (await revoke.Execute(new RevokeUserCommand(userId, null), ct))
            .Match(_ => NoContent(), Problem);

    /// <summary>Reactiva a un operador que estaba revocado.</summary>
    [HttpPost("{userid:guid}/reinstate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reinstate([FromRoute(Name = "userid")] Guid userId, CancellationToken ct)
        => (await reinstate.Execute(new ReinstateUserCommand(userId, null), ct))
            .Match(_ => NoContent(), Problem);

    /// <summary>Cuerpo del <c>POST /operator-users</c>.</summary>
    public sealed record ProvisionOperatorBody(string Email);
}
