using NovaFE.Domain.Common;

namespace NovaFE.Application.Sequences.ReadModels;

/// <summary>
/// Lo que la API devuelve para un rango de secuencias. Los campos derivados
/// (<see cref="Remaining"/>, <see cref="Capacity"/>, <see cref="IsLowStock"/>) los
/// calcula la consulta o este record; no se almacenan.
/// </summary>
public sealed record NcfSequenceView(
    Guid Id,
    string Environment,
    int Type,
    string Series,
    long RangeFrom,
    long RangeTo,
    long Next,
    long Capacity,
    long Remaining,
    DateOnly? ExpiresOn,
    bool Active,
    DateTimeOffset CreatedAt)
{
    /// <summary>Nombre del tipo de comprobante de cara al contribuyente.</summary>
    public string TypeName => EcfType.FromCodeOrDefault(Type)?.DisplayName ?? $"Tipo {Type}";

    /// <summary>El stock cayó al 20 % o menos del rango autorizado (RF-07.3).</summary>
    public bool IsLowStock => Remaining <= (long)Math.Ceiling(Capacity * 0.20);
}
