namespace NovaFE.Application.Organizations.SuspendOrganization;

/// <summary>Suspende una organización — bloquea en cascada a todos sus tenants. Recurso de operador.</summary>
public sealed record SuspendOrganizationCommand(Guid OrganizationId);
