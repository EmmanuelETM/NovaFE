namespace NovaFE.Application.Sequences.AllocateNcf;

/// <summary>
/// Toma la siguiente secuencia e-NCF del inventario del contribuyente para un tipo
/// y ambiente. Es una operación con efecto: avanza el puntero del rango.
/// </summary>
public sealed record AllocateNcfCommand(string Environment, int Type);
