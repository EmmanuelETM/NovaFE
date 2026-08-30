namespace NovaFE.Application.Sequences.RegisterSequenceRange;

/// <summary>
/// Registra un rango de e-NCF autorizado por la DGII para el contribuyente actual.
/// <paramref name="AuthorizedOn"/> es opcional: si no viene, se asume hoy.
/// </summary>
public sealed record RegisterSequenceRangeCommand(
    string Environment,
    int Type,
    string Series,
    long RangeFrom,
    long RangeTo,
    DateOnly? AuthorizedOn = null);
