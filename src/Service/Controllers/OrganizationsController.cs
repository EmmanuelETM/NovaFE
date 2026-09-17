using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
using NovaFE.Application.Tenants.Contracts;
using NovaFE.Domain.Common;
using NovaFE.Service.Common;
using NovaFE.Service.Security;

namespace NovaFE.Service.Controllers;

/// <summary>
/// Alta y consulta de organizaciones, la membresía de sus usuarios y los
/// contribuyentes (tenants/"proyectos") que agrupan. Es un recurso de
/// <b>operador</b> del SaaS: se autentica con la clave de operador (header
/// <c>X-Admin-Key</c>), no con un tenant — el self-service de organización
/// desde el dashboard (Fase 2) reusará estos casos de uso detrás de un
/// esquema de autorización distinto.
/// </summary>
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize(Policy = SecurityPolicies.Operator)]
public sealed class OrganizationsController(
    RegisterOrganizationUseCase register,
    GetOrganizationUseCase get,
    ListOrganizationsUseCase list,
    AddOrganizationMemberUseCase addMember,
    ListOrganizationMembersUseCase listMembers,
    ChangeOrganizationMemberRoleUseCase changeMemberRole,
    RemoveOrganizationMemberUseCase removeMember,
    AssignTenantToOrganizationUseCase assignTenant,
    ListOrganizationTenantsUseCase listTenants) : ApiController
{
    [HttpPost]
    public async Task<IActionResult> Register(
        [FromBody] RegisterOrganizationCommand command,
        CancellationToken ct)
        => (await register.Execute(command, ct)).Match(
            id => CreatedAtAction(nameof(GetById), new { id, version = "1" }, new { id }),
            Problem);

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(OrganizationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => (await get.Execute(new GetOrganizationQuery(id), ct)).Match(Ok, Problem);

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<OrganizationSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] ListOrganizationsQuery query,
        CancellationToken ct)
        => (await list.Execute(query, ct)).Match(Ok, Problem);

    /// <summary>Agrega un miembro a la organización, por correo. El usuario debe existir ya en la plataforma.</summary>
    [HttpPost("{id:guid}/members")]
    [ProducesResponseType(typeof(OrganizationMemberDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
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

    /// <summary>Los miembros de la organización.</summary>
    [HttpGet("{id:guid}/members")]
    [ProducesResponseType(typeof(IReadOnlyList<OrganizationMemberDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListMembers(Guid id, CancellationToken ct)
        => (await listMembers.Execute(new ListOrganizationMembersQuery(id), ct)).Match(Ok, Problem);

    /// <summary>Cambia el rol de un miembro de la organización.</summary>
    [HttpPatch("{id:guid}/members/{userid:guid}")]
    [ProducesResponseType(typeof(OrganizationMemberDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChangeMemberRole(
        Guid id,
        [FromRoute(Name = "userid")] Guid userId,
        [FromBody] ChangeMemberRoleBody body,
        CancellationToken ct)
        => (await changeMemberRole.Execute(new ChangeOrganizationMemberRoleCommand(id, userId, body.Role), ct))
            .Match(Ok, Problem);

    /// <summary>Quita a un miembro de la organización.</summary>
    [HttpDelete("{id:guid}/members/{userid:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveMember(
        Guid id,
        [FromRoute(Name = "userid")] Guid userId,
        CancellationToken ct)
        => (await removeMember.Execute(new RemoveOrganizationMemberCommand(id, userId), ct))
            .Match(_ => NoContent(), Problem);

    /// <summary>Asocia (o reasocia) un contribuyente existente a la organización.</summary>
    [HttpPost("{id:guid}/tenants/{tenantid:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignTenant(
        Guid id,
        [FromRoute(Name = "tenantid")] Guid tenantId,
        CancellationToken ct)
        => (await assignTenant.Execute(new AssignTenantToOrganizationCommand(id, tenantId), ct))
            .Match(_ => NoContent(), Problem);

    /// <summary>Los contribuyentes (tenants/"proyectos") de la organización.</summary>
    [HttpGet("{id:guid}/tenants")]
    [ProducesResponseType(typeof(PagedResult<TenantSummaryDto>), StatusCodes.Status200OK)]
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
