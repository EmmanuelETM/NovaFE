namespace NovaFE.Application.Tenants.ActivateTenant;

/// <summary>Reactiva un tenant suspendido. Recurso de operador.</summary>
public sealed record ActivateTenantCommand(Guid TenantId);
