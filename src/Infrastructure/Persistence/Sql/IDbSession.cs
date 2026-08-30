using System.Data.Common;

namespace NovaFE.Infrastructure.Persistence.Sql;

/// <summary>
/// Conexión SQL compartida por todos los repositorios Dapper dentro del scope de
/// la petición, junto con la transacción activa si hay una.
/// <para>
/// Es lo que hace que <c>IUnitOfWork</c> funcione con Dapper: los repositorios no
/// abren su propia conexión, piden la de la sesión y le pasan
/// <see cref="Transaction"/> a Dapper. Así varias escrituras caen en la misma
/// transacción sin que el caso de uso tenga que enterarse.
/// </para>
/// <para>
/// Esta carpeta se llama <c>Sql</c> y no <c>Dapper</c> a propósito: un namespace
/// que termine en <c>.Dapper</c> colisiona con el del paquete y rompe
/// <c>using Dapper;</c> dentro de estos archivos.
/// </para>
/// </summary>
/// <example>
/// <code>
/// var conexion = await _session.GetConnectionAsync(ct);
/// return await conexion.QueryAsync&lt;SolicitudDto&gt;(sql, parametros, _session.Transaction);
/// </code>
/// </example>
public interface IDbSession
{
    /// <summary>Devuelve la conexión abierta del scope, creándola la primera vez.</summary>
    Task<DbConnection> GetConnectionAsync(CancellationToken ct = default);

    /// <summary>Transacción activa, o null si la operación no está dentro de una.</summary>
    DbTransaction? Transaction { get; }
}
