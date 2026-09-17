namespace NovaFE.Application.Sequences.DeactivateSequenceRange;

/// <summary>
/// Desactiva un rango de e-NCF del contribuyente actual — típicamente uno ya
/// agotado, para poder registrar uno nuevo con la misma serie
/// (<c>RegisterSequenceRangeUseCase</c> rechaza un alta si ya hay un rango
/// activo para esa serie/tipo/ambiente).
/// </summary>
public sealed record DeactivateSequenceRangeCommand(Guid Id);
