namespace NovaFE.Application.Organizations.ActivateOrganization;

/// <summary>Reactiva una organización suspendida. Recurso de operador.</summary>
public sealed record ActivateOrganizationCommand(Guid OrganizationId);
