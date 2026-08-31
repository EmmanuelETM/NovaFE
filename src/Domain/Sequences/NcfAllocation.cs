namespace NovaFE.Domain.Sequences;

/// <summary>
/// El resultado de asignar una secuencia e-NCF: el número entregado y el
/// vencimiento del rango del que salió (para <c>&lt;FechaVencimientoSecuencia&gt;</c>).
/// </summary>
public sealed record NcfAllocation(Encf Encf, DateOnly? SequenceExpiresOn);
