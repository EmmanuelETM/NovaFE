namespace NovaFE.Application.Organizations.ChangeOrganizationMemberRole;

public sealed record ChangeOrganizationMemberRoleCommand(Guid OrganizationId, Guid PlatformUserId, string Role);
