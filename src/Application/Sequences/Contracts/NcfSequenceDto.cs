using NovaFE.Domain.Common;

namespace NovaFE.Application.Sequences.Contracts;

/// <summary>
/// Lo que la API devuelve para un rango de secuencias. Los campos derivados
/// (<see cref="Remaining"/>, <see cref="Capacity"/>, <see cref="IsLowStock"/>) los
/// calcula la consulta — <see cref="IsLowStock"/> con el setting runtime
/// <c>sequences.low_stock_fraction</c> (RF-07.3); no se almacenan.
/// </summary>
public sealed record NcfSequenceDto(
    Guid Id,
    string Environment,
    int Type,
    string Series,
    long RangeFrom,
    long RangeTo,
    long Next,
    long Capacity,
    long Remaining,
    bool IsLowStock,
    DateOnly? ExpiresOn,
    bool Active,
    DateTimeOffset CreatedAt)
{
    /// <summary>Nombre del tipo de comprobante de cara al contribuyente.</summary>
    public string TypeName => EcfType.FromCodeOrDefault(Type)?.DisplayName ?? $"Tipo {Type}";
}
