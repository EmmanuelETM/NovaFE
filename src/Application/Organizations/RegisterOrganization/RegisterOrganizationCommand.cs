namespace NovaFE.Application.Organizations.RegisterOrganization;

/// <summary>
/// Registers a new organization on the platform. Returns the new id.
/// <paramref name="OwnerEmail"/> es opcional: si viene, da de alta el
/// <see cref="Domain.Users.PlatformUser"/> (si no existe ya) y lo agrega como
/// <c>owner</c> en el mismo paso — onboarding estructural atómico (Fase 2).
/// </summary>
public sealed record RegisterOrganizationCommand(string Name, string Slug, string Plan, string? OwnerEmail = null);
