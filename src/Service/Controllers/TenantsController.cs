using Asp.Versioning;
using NovaFE.Application.Audit.ListAuditLog;
using NovaFE.Application.Tenants.ActivateTenant;
using NovaFE.Application.Tenants.Contracts;
using NovaFE.Application.Tenants.GetEmitterProfile;
using NovaFE.Application.Tenants.GetTenant;
using NovaFE.Application.Tenants.ListTenants;
using NovaFE.Application.Tenants.RegisterTenant;
using NovaFE.Application.Tenants.SetEmitterProfile;
using NovaFE.Application.Tenants.SuspendTenant;
using NovaFE.Application.Users.ChangeUserRole;
using NovaFE.Application.Users.Contracts;
using NovaFE.Application.Users.ListTenantUsers;
using NovaFE.Application.Users.ProvisionTenantUser;
using NovaFE.Application.Users.ReinstateUser;
using NovaFE.Application.Users.RevokeUser;
using NovaFE.Domain.Common;
using NovaFE.Service.Common;
using NovaFE.Service.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NovaFE.Service.Controllers;

/// <summary>
/// Alta y consulta de contribuyentes, su perfil fiscal de emisor y sus API keys.
/// Es un recurso de <b>operador</b> del SaaS: se autentica con la clave de
/// operador (header <c>X-Admin-Key</c>), no con un tenant.
/// </summary>
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize(Policy = SecurityPolicies.Operator)]
public sealed class TenantsController(
    RegisterTenantUseCase register,
    GetTenantUseCase get,
    ListTenantsUseCase list,
    GetEmitterProfileUseCase getEmitterProfile,
    SetEmitterProfileUseCase setEmitterProfile,
    SuspendTenantUseCase suspendTenant,
    ActivateTenantUseCase activateTenant,
    ProvisionTenantUserUseCase provisionUser,
    ListTenantUsersUseCase listUsers,
    RevokeUserUseCase revokeUser,
    ChangeUserRoleUseCase changeUserRole,
    ReinstateUserUseCase reinstateUser,
    ListAuditLogUseCase listAuditLog,
    CurrentTenant currentTenant) : ApiController
{
    [HttpPost]
    public async Task<IActionResult> Register(
        [FromBody] RegisterTenantCommand command,
        CancellationToken ct)
        => (await register.Execute(command, ct)).Match(
            id => CreatedAtAction(nameof(GetById), new { id, version = "1" }, new { id }),
            Problem);

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(TenantDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => (await get.Execute(new GetTenantQuery(id), ct)).Match(Ok, Problem);

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<TenantSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] ListTenantsQuery query,
        CancellationToken ct)
        => (await list.Execute(query, ct)).Match(Ok, Problem);

    /// <summary>
    /// El perfil fiscal del emisor (dirección, ubicación, teléfonos, ambiente),
    /// como operador. <c>currentTenant.Set</c> es <c>internal</c>, llamable
    /// porque este controller vive en el mismo ensamblado que
    /// <see cref="CurrentTenant"/>; con eso fijado, el caso de uso —compartido
    /// con <c>EmitterProfileController</c> self-service— no se entera de quién
    /// lo llamó. Ver el comentario de <c>CertificatesController.UploadForTenant</c>
    /// para la nota sobre RLS.
    /// </summary>
    [HttpGet("{id:guid}/emitter-profile")]
    [ProducesResponseType(typeof(EmitterProfileDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEmitterProfile(Guid id, CancellationToken ct)
    {
        currentTenant.Set(id);
        return (await getEmitterProfile.Execute(ct)).Match(Ok, Problem);
    }

    /// <summary>Crea o reemplaza el perfil fiscal del emisor (upsert), como operador.</summary>
    [HttpPut("{id:guid}/emitter-profile")]
    [ProducesResponseType(typeof(EmitterProfileDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetEmitterProfile(
        Guid id,
        [FromBody] SetEmitterProfileBody body,
        CancellationToken ct)
    {
        currentTenant.Set(id);
        return (await setEmitterProfile.Execute(
                new SetEmitterProfileCommand(
                    body.Address,
                    body.Municipality,
                    body.Province,
                    body.Phones,
                    body.Email,
                    body.EconomicActivity,
                    body.DefaultEnvironment),
                ct))
            .Match(Ok, Problem);
    }

    /// <summary>
    /// Suspende el tenant: bloquea autenticación (API keys y dashboard) y
    /// emisión de inmediato. No pago, abuso, o requerimiento legal.
    /// </summary>
    [HttpPost("{id:guid}/suspend")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Suspend(Guid id, CancellationToken ct)
        => (await suspendTenant.Execute(new SuspendTenantCommand(id), ct)).Match(_ => NoContent(), Problem);

    /// <summary>Reactiva un tenant suspendido.</summary>
    [HttpPost("{id:guid}/activate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
        => (await activateTenant.Execute(new ActivateTenantCommand(id), ct)).Match(_ => NoContent(), Problem);

    /// <summary>
    /// Da de alta a un empleado del contribuyente en el dashboard, por correo. La
    /// persona entra cuando inicia sesión con ese mismo correo en Better Auth.
    /// </summary>
    [HttpPost("{id:guid}/users")]
    [ProducesResponseType(typeof(PlatformUserDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ProvisionUser(
        Guid id,
        [FromBody] ProvisionUserBody body,
        CancellationToken ct)
        => (await provisionUser.Execute(new ProvisionTenantUserCommand(id, body.Email, body.Role), ct))
            .Match(
                user => CreatedAtAction(nameof(ListUsers), new { id, version = "1" }, user),
                Problem);

    /// <summary>Los usuarios del contribuyente en el dashboard.</summary>
    [HttpGet("{id:guid}/users")]
    [ProducesResponseType(typeof(IReadOnlyList<PlatformUserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListUsers(Guid id, CancellationToken ct)
        => (await listUsers.Execute(new ListTenantUsersQuery(id), ct)).Match(Ok, Problem);

    /// <summary>Cambia el rol de un usuario del contribuyente.</summary>
    [HttpPatch("{id:guid}/users/{userid:guid}")]
    [ProducesResponseType(typeof(PlatformUserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChangeUserRole(
        Guid id,
        [FromRoute(Name = "userid")] Guid userId,
        [FromBody] ChangeRoleBody body,
        CancellationToken ct)
        => (await changeUserRole.Execute(new ChangeUserRoleCommand(userId, id, body.Role), ct))
            .Match(Ok, Problem);

    /// <summary>Revoca el acceso de un usuario del contribuyente al dashboard.</summary>
    [HttpDelete("{id:guid}/users/{userid:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RevokeUser(
        Guid id,
        [FromRoute(Name = "userid")] Guid userId,
        CancellationToken ct)
        => (await revokeUser.Execute(new RevokeUserCommand(userId, id), ct))
            .Match(_ => NoContent(), Problem);

    /// <summary>Reactiva a un usuario del contribuyente que estaba revocado.</summary>
    [HttpPost("{id:guid}/users/{userid:guid}/reinstate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ReinstateUser(
        Guid id,
        [FromRoute(Name = "userid")] Guid userId,
        CancellationToken ct)
        => (await reinstateUser.Execute(new ReinstateUserCommand(userId, id), ct))
            .Match(_ => NoContent(), Problem);

    /// <summary>Registro de auditoría del contribuyente (RF-14.4), paginado.</summary>
    [HttpGet("{id:guid}/audit-log")]
    public async Task<IActionResult> GetAuditLog(
        Guid id,
        [FromQuery] int page,
        [FromQuery] int pageSize,
        CancellationToken ct)
        => (await listAuditLog.Execute(new ListAuditLogQuery(id) { Page = page, PageSize = pageSize }, ct))
            .Match(Ok, Problem);

    /// <summary>Cuerpo del <c>PUT .../emitter-profile</c> (el contribuyente va en la ruta).</summary>
    public sealed record SetEmitterProfileBody(
        string Address,
        string? Municipality,
        string? Province,
        IReadOnlyList<string>? Phones,
        string? Email,
        string? EconomicActivity,
        string DefaultEnvironment);

    /// <summary>
    /// Cuerpo del <c>POST .../users</c>. <c>role</c> (<c>admin_tenant</c> /
    /// <c>emisor</c> / <c>consultor</c>) es obligatorio; <c>admin_sistema</c> no
    /// es un rol de contribuyente.
    /// </summary>
    public sealed record ProvisionUserBody(string Email, string Role);

    /// <summary>Cuerpo del <c>PATCH .../users/{userId}</c>.</summary>
    public sealed record ChangeRoleBody(string Role);
}
