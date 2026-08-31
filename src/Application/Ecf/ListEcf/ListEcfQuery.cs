using NovaFE.Application.Ecf.Contracts;
using NovaFE.Domain.Common;

namespace NovaFE.Application.Ecf.ListEcf;

/// <summary>Listado paginado de los comprobantes emitidos del tenant actual.</summary>
public sealed record ListEcfQuery : PagedRequest
{
    public int? Type { get; init; }
    public string? Status { get; init; }
    public string? Search { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }

    public EcfListFilter ToFilter() => new()
    {
        Page = Page,
        PageSize = PageSize,
        Type = Type,
        Status = Status,
        Search = Search,
        From = From,
        To = To,
    };
}
