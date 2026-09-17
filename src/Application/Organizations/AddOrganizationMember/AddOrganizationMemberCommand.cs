namespace NovaFE.Application.Organizations.AddOrganizationMember;

/// <summary>
/// Agrega un miembro a la organización, por correo. Recurso de operador. El
/// usuario debe existir ya como <c>PlatformUser</c> (dado de alta por
/// <c>/tenants/{id}/users</c> u <c>/operator-users</c>) — Fase 1 no crea la
/// identidad, solo la membresía.
/// </summary>
public sealed record AddOrganizationMemberCommand(Guid OrganizationId, string Email, string Role);
