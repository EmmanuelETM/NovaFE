using NovaFE.Domain.Common;

namespace NovaFE.Application.Webhooks.ListDeadDeliveries;

/// <summary>
/// Entregas <c>dead</c> de toda la plataforma, paginado — explícitamente sin
/// tenant, para el panel de operador (<c>webhook_deliveries</c> es tabla de
/// sistema, sin RLS).
/// </summary>
public sealed record ListDeadDeliveriesQuery : PagedRequest;
