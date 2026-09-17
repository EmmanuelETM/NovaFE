namespace NovaFE.Application.Organizations.UpdateOrganizationPlan;

public sealed record UpdateOrganizationPlanCommand(Guid OrganizationId, string Plan);
