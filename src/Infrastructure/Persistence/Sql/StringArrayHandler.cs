using System.Data;
using Dapper;

namespace NovaFE.Infrastructure.Persistence.Sql;

/// <summary>
/// Npgsql ya materializa las columnas <c>text[]</c> como <see cref="string"/><c>[]</c>,
/// pero la coincidencia de constructor de Dapper ve el tipo del lector como
/// <see cref="Array"/> y no casa con un parámetro <c>string[]</c> del record de
/// lectura. Este handler cierra la brecha (y normaliza <c>NULL</c> a un arreglo vacío).
/// </summary>
internal sealed class StringArrayHandler : SqlMapper.TypeHandler<string[]>
{
    public override string[] Parse(object value) => value switch
    {
        string[] array => array,
        null or DBNull => [],
        IEnumerable<object> items => [.. items.Select(item => item?.ToString() ?? string.Empty)],
        _ => throw new InvalidCastException(
            $"No se puede convertir un valor de tipo '{value.GetType().Name}' a string[]."),
    };

    public override void SetValue(IDbDataParameter parameter, string[]? value)
        => parameter.Value = value ?? (object)DBNull.Value;
}
