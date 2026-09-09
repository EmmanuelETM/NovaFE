using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NovaFE.Application.Users.ListOperatorUsers;
using NovaFE.Application.Users.ProvisionOperatorUser;
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
    RevokeUserUseCase revoke) : ApiController
{
    [HttpPost]
    public async Task<IActionResult> Provision([FromBody] ProvisionOperatorBody body, CancellationToken ct)
        => (await provision.Execute(new ProvisionOperatorUserCommand(body.Email), ct))
            .Match(user => CreatedAtAction(nameof(List), new { version = "1" }, user), Problem);

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => (await list.Execute(ct)).Match(Ok, Problem);

    [HttpDelete("{userid:guid}")]
    public async Task<IActionResult> Revoke([FromRoute(Name = "userid")] Guid userId, CancellationToken ct)
        => (await revoke.Execute(new RevokeUserCommand(userId, null), ct))
            .Match(_ => NoContent(), Problem);

    /// <summary>Cuerpo del <c>POST /operator-users</c>.</summary>
    public sealed record ProvisionOperatorBody(string Email);
}
