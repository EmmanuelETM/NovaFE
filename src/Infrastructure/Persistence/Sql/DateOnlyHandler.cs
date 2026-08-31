using System.Data;
using Dapper;

namespace NovaFE.Infrastructure.Persistence.Sql;

/// <summary>
/// Dapper no sabe enlazar <see cref="DateOnly"/> como valor de parámetro ni
/// materializarlo desde una columna <c>date</c>; este handler cierra ambas brechas.
/// </summary>
internal sealed class DateOnlyHandler : SqlMapper.TypeHandler<DateOnly>
{
    public override DateOnly Parse(object value) => value switch
    {
        DateOnly date => date,
        DateTime dateTime => DateOnly.FromDateTime(dateTime),
        string text => DateOnly.Parse(text, System.Globalization.CultureInfo.InvariantCulture),
        _ => throw new InvalidCastException(
            $"No se puede convertir un valor de tipo '{value?.GetType().Name ?? "null"}' a DateOnly."),
    };

    public override void SetValue(IDbDataParameter parameter, DateOnly value)
    {
        parameter.DbType = DbType.Date;
        parameter.Value = value;
    }
}
