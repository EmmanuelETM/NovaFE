using System.Data;
using System.Text.Json;
using Dapper;
using NovaFE.Domain.Ecf;

namespace NovaFE.Infrastructure.Ecf.EfCore;

/// <summary>
/// Materializa la columna <c>jsonb</c> <c>issued_ecf.totals</c> (que Npgsql
/// devuelve como <see cref="string"/>) en <see cref="EcfTotalsSnapshot"/> para las
/// lecturas Dapper.
/// </summary>
internal sealed class EcfTotalsSnapshotJsonHandler : SqlMapper.TypeHandler<EcfTotalsSnapshot>
{
    public override EcfTotalsSnapshot Parse(object value) => value switch
    {
        string json => JsonSerializer.Deserialize<EcfTotalsSnapshot>(json)
            ?? throw new InvalidOperationException("El JSON de totales del comprobante es nulo."),
        _ => throw new InvalidCastException(
            $"No se puede convertir un valor de tipo '{value?.GetType().Name ?? "null"}' a EcfTotalsSnapshot."),
    };

    public override void SetValue(IDbDataParameter parameter, EcfTotalsSnapshot? value)
        => parameter.Value = value is null ? DBNull.Value : JsonSerializer.Serialize(value);
}
