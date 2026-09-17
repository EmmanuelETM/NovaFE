namespace NovaFE.Application.Tenants.SuspendTenant;

/// <summary>Suspende un tenant — bloquea autenticación y emisión de inmediato. Recurso de operador.</summary>
public sealed record SuspendTenantCommand(Guid TenantId);
