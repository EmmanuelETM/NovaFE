using System.Data;
using Dapper;

namespace NovaFE.Infrastructure.Persistence.Sql;

/// <summary>
/// Npgsql devuelve <see cref="DateTime"/> (UTC) para las columnas
/// <c>timestamp with time zone</c>. Los modelos de lectura exponen
/// <see cref="DateTimeOffset"/> para ser consistentes con el dominio, así que
/// Dapper necesita este handler para materializarlos.
/// </summary>
internal sealed class DateTimeOffsetHandler : SqlMapper.TypeHandler<DateTimeOffset>
{
    public override DateTimeOffset Parse(object value) => value switch
    {
        DateTimeOffset offset => offset,
        DateTime utc => new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)),
        _ => throw new InvalidCastException(
            $"No se puede convertir un valor de tipo '{value?.GetType().Name ?? "null"}' a DateTimeOffset."),
    };

    public override void SetValue(IDbDataParameter parameter, DateTimeOffset value)
        => parameter.Value = value;
}
