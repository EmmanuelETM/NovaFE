using System.Data;
using System.Text.Json;
using Dapper;
using NovaFE.Domain.Dgii;

namespace NovaFE.Infrastructure.Ecf.EfCore;

/// <summary>
/// Materializa la columna <c>jsonb</c> <c>issued_ecf.dgii_messages</c> (Npgsql la
/// devuelve como <see cref="string"/>) en la lista de <see cref="DgiiMessage"/>
/// para las lecturas Dapper.
/// </summary>
internal sealed class DgiiMessagesJsonHandler : SqlMapper.TypeHandler<IReadOnlyList<DgiiMessage>>
{
    public override IReadOnlyList<DgiiMessage> Parse(object value) => value switch
    {
        string json => JsonSerializer.Deserialize<IReadOnlyList<DgiiMessage>>(json) ?? [],
        null or DBNull => [],
        _ => throw new InvalidCastException(
            $"No se puede convertir un valor de tipo '{value.GetType().Name}' a una lista de DgiiMessage."),
    };

    public override void SetValue(IDbDataParameter parameter, IReadOnlyList<DgiiMessage>? value)
        => parameter.Value = value is null ? DBNull.Value : JsonSerializer.Serialize(value);
}
