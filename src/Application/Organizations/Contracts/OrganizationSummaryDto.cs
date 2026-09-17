namespace NovaFE.Application.Organizations.Contracts;

/// <summary>Row shape for the paged organization list.</summary>
public sealed record OrganizationSummaryDto(
    Guid Id,
    string Name,
    string Slug,
    string Plan,
    string Status);
