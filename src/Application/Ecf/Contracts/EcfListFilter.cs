using NovaFE.Domain.Common;

namespace NovaFE.Application.Ecf.Contracts;

/// <summary>
/// Filtros del listado de comprobantes emitidos del tenant actual. Hereda
/// <c>Page</c>/<c>PageSize</c>/<c>Skip</c> ya recortados de <see cref="PagedRequest"/>.
/// </summary>
public sealed record EcfListFilter : PagedRequest
{
    /// <summary>Código de tipo de e-CF (31, 32, …). Null = todos.</summary>
    public int? Type { get; init; }

    /// <summary>Estado del comprobante (<c>signed</c>, …). Null = todos.</summary>
    public string? Status { get; init; }

    /// <summary>Coincide con el e-NCF, el RNC del comprador o la razón social.</summary>
    public string? Search { get; init; }

    /// <summary>Fecha de emisión desde (inclusive).</summary>
    public DateOnly? From { get; init; }

    /// <summary>Fecha de emisión hasta (inclusive).</summary>
    public DateOnly? To { get; init; }
}
