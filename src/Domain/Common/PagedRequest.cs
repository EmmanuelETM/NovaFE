namespace NovaFE.Domain.Common;

/// <summary>
/// Contraparte de entrada de <see cref="PagedResult{T}"/>. Hereda de este record
/// en los requests de tus queries paginadas y obtienes <c>Page</c>, <c>PageSize</c>
/// y <c>Skip</c> ya resueltos.
/// <para>
/// Los valores se <b>ajustan</b> en lugar de rechazarse: un <c>pageSize=100000</c>
/// se recorta a <see cref="MaxPageSize"/> en vez de devolver 400, y así ninguna
/// consulta puede pedir la tabla completa por accidente.
/// </para>
/// </summary>
/// <example>
/// <code>
/// public record ListarSolicitudes(string? Filtro = null) : PagedRequest;
/// </code>
/// </example>
public record PagedRequest
{
    public const int MaxPageSize = 100;
    public const int DefaultPageSize = 20;

    private readonly int _page = 1;
    private readonly int _pageSize = DefaultPageSize;

    /// <summary>Página solicitada, empezando en 1.</summary>
    public int Page
    {
        get => _page;
        init => _page = value < 1 ? 1 : value;
    }

    /// <summary>Cantidad de elementos por página. Se recorta a <see cref="MaxPageSize"/>.</summary>
    public int PageSize
    {
        get => _pageSize;
        init => _pageSize = value < 1 ? DefaultPageSize : Math.Min(value, MaxPageSize);
    }

    /// <summary>Elementos a omitir. Úsalo directamente en OFFSET o en <c>Skip()</c>.</summary>
    public int Skip => (Page - 1) * PageSize;
}
