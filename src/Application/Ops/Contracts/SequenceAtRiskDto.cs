namespace NovaFE.Application.Ops.Contracts;

/// <summary>
/// Un rango de secuencia e-NCF de un tenant, ya bajo
/// <c>sequences.low_stock_fraction</c> (misma definición que usa el propio
/// tenant en <c>GET /sequences</c>) o agotado. El agregado cross-tenant
/// ("4 activas, 1 baja") no le dice a un operador a quién avisar — esto sí.
/// </summary>
public sealed record SequenceAtRiskDto(string TenantName, string Type, long Remaining);
