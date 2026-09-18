namespace NovaFE.Application.Organizations.UnassignTenant;

/// <summary>Desasocia un contribuyente de la organización — vuelve a quedar huérfano. Recurso de operador.</summary>
public sealed record UnassignTenantFromOrganizationCommand(Guid OrganizationId, Guid TenantId);
