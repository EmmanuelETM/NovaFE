namespace NovaFE.Application.Organizations.AssignTenant;

/// <summary>Asocia (o reasocia) un contribuyente existente a una organización. Recurso de operador.</summary>
public sealed record AssignTenantToOrganizationCommand(Guid OrganizationId, Guid TenantId);
