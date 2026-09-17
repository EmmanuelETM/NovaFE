namespace NovaFE.Application.Organizations.Contracts;

/// <summary>Vista de un miembro de la organización para el listado de membresía.</summary>
public sealed record OrganizationMemberDto(
    Guid PlatformUserId,
    string Email,
    string Role,
    DateTimeOffset CreatedAt);
