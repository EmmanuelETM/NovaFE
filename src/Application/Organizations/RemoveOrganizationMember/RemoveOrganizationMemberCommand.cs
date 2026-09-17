namespace NovaFE.Application.Organizations.RemoveOrganizationMember;

public sealed record RemoveOrganizationMemberCommand(Guid OrganizationId, Guid PlatformUserId);
