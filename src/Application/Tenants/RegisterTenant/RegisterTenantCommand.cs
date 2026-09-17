namespace NovaFE.Application.Tenants.RegisterTenant;

/// <summary>
/// Registers a new tenant (contribuyente) on the platform. Returns the new id.
/// El plan/cuota vive en la Organization, no acá (Fase 2).
/// </summary>
public sealed record RegisterTenantCommand(
    string Rnc,
    string LegalName,
    string? TradeName);
