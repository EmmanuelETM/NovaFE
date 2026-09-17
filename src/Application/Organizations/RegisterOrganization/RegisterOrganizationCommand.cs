namespace NovaFE.Application.Organizations.RegisterOrganization;

/// <summary>Registers a new organization on the platform. Returns the new id.</summary>
public sealed record RegisterOrganizationCommand(string Name, string Slug);
