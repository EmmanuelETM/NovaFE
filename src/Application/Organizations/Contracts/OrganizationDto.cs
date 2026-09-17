namespace NovaFE.Application.Organizations.Contracts;

/// <summary>Full view of an organization, returned by the detail query.</summary>
public sealed record OrganizationDto(
    Guid Id,
    string Name,
    string Slug,
    string Plan,
    string Status,
    DateTimeOffset CreatedAt);
