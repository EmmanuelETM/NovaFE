namespace NovaFE.Application.Tenants.RegisterTenant;

/// <summary>
/// Registers a new tenant (contribuyente) on the platform. Returns the new id.
/// </summary>
public sealed record RegisterTenantCommand(
    string Rnc,
    string LegalName,
    string? TradeName,
    string Plan);
