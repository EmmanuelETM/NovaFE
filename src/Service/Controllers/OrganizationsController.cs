using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NovaFE.Application.Organizations.ActivateOrganization;
using NovaFE.Application.Organizations.AddOrganizationMember;
using NovaFE.Application.Organizations.AssignTenant;
using NovaFE.Application.Organizations.ChangeOrganizationMemberRole;
using NovaFE.Application.Organizations.Contracts;
using NovaFE.Application.Organizations.GetOrganization;
using NovaFE.Application.Organizations.ListOrganizationMembers;
using NovaFE.Application.Organizations.ListOrganizations;
using NovaFE.Application.Organizations.ListOrganizationTenants;
using NovaFE.Application.Organizations.RegisterOrganization;
using NovaFE.Application.Organizations.RemoveOrganizationMember;
using NovaFE.Application.Organizations.SuspendOrganization;
using NovaFE.Domain.Common;
using NovaFE.Service.Common;
using NovaFE.Service.Security;

namespace NovaFE.Service.Controllers;

/// <summary>
/// Alta y consulta de organizaciones, la membresía de sus usuarios y los
/// contribuyentes (tenants/"proyectos") que agrupan. El alta, el listado
/// administrativo y la suspensión son de <b>operador</b>
/// (<c>X-Admin-Key</c>) — gobernanza de la plataforma. La gestión de
/// miembros (invitar, listar, cambiar rol, quitar) es
/// <b>self-service</b> para el <c>owner</c>/<c>admin</c> de esa organización
/// puntual, resuelto en el caso de uso (<see cref="Application.Organizations.OrganizationAccess"/>)
/// porque el rol varía por organización y no es un claim fijo del principal —
/// el operador conserva acceso a todas, para soporte. Ver
/// <c>docs/multi-tenancy-hierarchy.md</c>.
/// </summary>
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/[controller]")]
public sealed class OrganizationsController(
    RegisterOrganizationUseCase register,
    GetOrganizationUseCase get,
    ListOrganizationsUseCase list,
    SuspendOrganizationUseCase suspend,
    ActivateOrganizationUseCase activate,
    AddOrganizationMemberUseCase addMember,
    ListOrganizationMembersUseCase listMembers,
    ChangeOrganizationMemberRoleUseCase changeMemberRole,
    RemoveOrganizationMemberUseCase removeMember,
    AssignTenantToOrganizationUseCase assignTenant,
    ListOrganizationTenantsUseCase listTenants) : ApiController
{
    [HttpPost]
    [Authorize(Policy = SecurityPolicies.Operator)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterOrganizationCommand command,
        CancellationToken ct)
        => (await register.Execute(command, ct)).Match(
            id => CreatedAtAction(nameof(GetById), new { id, version = "1" }, new { id }),
            Problem);

    [HttpGet("{id:guid}")]
    [Authorize(Policy = SecurityPolicies.Operator)]
    [ProducesResponseType(typeof(OrganizationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => (await get.Execute(new GetOrganizationQuery(id), ct)).Match(Ok, Problem);

    [HttpGet]
    [Authorize(Policy = SecurityPolicies.Operator)]
    [ProducesResponseType(typeof(PagedResult<OrganizationSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] ListOrganizationsQuery query,
        CancellationToken ct)
        => (await list.Execute(query, ct)).Match(Ok, Problem);

    /// <summary>Suspende la organización — bloquea en cascada a todos sus tenants.</summary>
    [HttpPost("{id:guid}/suspend")]
    [Authorize(Policy = SecurityPolicies.Operator)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Suspend(Guid id, CancellationToken ct)
        => (await suspend.Execute(new SuspendOrganizationCommand(id), ct)).Match(_ => NoContent(), Problem);

    /// <summary>Reactiva una organización suspendida.</summary>
    [HttpPost("{id:guid}/activate")]
    [Authorize(Policy = SecurityPolicies.Operator)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
        => (await activate.Execute(new ActivateOrganizationCommand(id), ct)).Match(_ => NoContent(), Problem);

    /// <summary>
    /// Agrega un miembro a la organización, por correo. Self-service para
    /// <c>owner</c>/<c>admin</c> de esta organización (o el operador). El
    /// usuario debe existir ya en la plataforma.
    /// </summary>
    [HttpPost("{id:guid}/members")]
    [Authorize(Policy = SecurityPolicies.Authenticated)]
    [ProducesResponseType(typeof(OrganizationMemberDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddMember(
        Guid id,
        [FromBody] AddMemberBody body,
        CancellationToken ct)
        => (await addMember.Execute(new AddOrganizationMemberCommand(id, body.Email, body.Role), ct))
            .Match(
                member => CreatedAtAction(nameof(ListMembers), new { id, version = "1" }, member),
                Problem);

    /// <summary>Los miembros de la organización. Self-service para cualquier miembro (o el operador).</summary>
    [HttpGet("{id:guid}/members")]
    [Authorize(Policy = SecurityPolicies.Authenticated)]
    [ProducesResponseType(typeof(IReadOnlyList<OrganizationMemberDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListMembers(Guid id, CancellationToken ct)
        => (await listMembers.Execute(new ListOrganizationMembersQuery(id), ct)).Match(Ok, Problem);

    /// <summary>Cambia el rol de un miembro. Self-service para <c>owner</c>/<c>admin</c> de esta organización (o el operador).</summary>
    [HttpPatch("{id:guid}/members/{userid:guid}")]
    [Authorize(Policy = SecurityPolicies.Authenticated)]
    [ProducesResponseType(typeof(OrganizationMemberDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ChangeMemberRole(
        Guid id,
        [FromRoute(Name = "userid")] Guid userId,
        [FromBody] ChangeMemberRoleBody body,
        CancellationToken ct)
        => (await changeMemberRole.Execute(new ChangeOrganizationMemberRoleCommand(id, userId, body.Role), ct))
            .Match(Ok, Problem);

    /// <summary>Quita a un miembro. Self-service para <c>owner</c>/<c>admin</c> de esta organización (o el operador).</summary>
    [HttpDelete("{id:guid}/members/{userid:guid}")]
    [Authorize(Policy = SecurityPolicies.Authenticated)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RemoveMember(
        Guid id,
        [FromRoute(Name = "userid")] Guid userId,
        CancellationToken ct)
        => (await removeMember.Execute(new RemoveOrganizationMemberCommand(id, userId), ct))
            .Match(_ => NoContent(), Problem);

    /// <summary>
    /// Asocia (o reasocia) un contribuyente existente a la organización.
    /// Operador: crear/asociar tenants sigue siendo estructural (ver
    /// "abierto" en <c>docs/multi-tenancy-hierarchy.md</c>).
    /// </summary>
    [HttpPost("{id:guid}/tenants/{tenantid:guid}")]
    [Authorize(Policy = SecurityPolicies.Operator)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignTenant(
        Guid id,
        [FromRoute(Name = "tenantid")] Guid tenantId,
        CancellationToken ct)
        => (await assignTenant.Execute(new AssignTenantToOrganizationCommand(id, tenantId), ct))
            .Match(_ => NoContent(), Problem);

    /// <summary>Los contribuyentes (tenants/"proyectos") de la organización. Self-service para cualquier miembro (o el operador).</summary>
    [HttpGet("{id:guid}/tenants")]
    [Authorize(Policy = SecurityPolicies.Authenticated)]
    [ProducesResponseType(typeof(PagedResult<OrganizationTenantSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListTenants(
        Guid id,
        [FromQuery] int page,
        [FromQuery] int pageSize,
        CancellationToken ct)
        => (await listTenants.Execute(new ListOrganizationTenantsQuery(id) { Page = page, PageSize = pageSize }, ct))
            .Match(Ok, Problem);

    /// <summary>Cuerpo del <c>POST .../members</c>.</summary>
    public sealed record AddMemberBody(string Email, string Role);

    /// <summary>Cuerpo del <c>PATCH .../members/{userId}</c>.</summary>
    public sealed record ChangeMemberRoleBody(string Role);
}
