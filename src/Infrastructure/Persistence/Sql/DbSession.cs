using System.Data.Common;
using Npgsql;
using Microsoft.Extensions.Options;

namespace NovaFE.Infrastructure.Persistence.Sql;

/// <summary>
/// Implementación de <see cref="IDbSession"/> con vida de scope (una por petición).
/// La conexión se abre de forma diferida: una petición que no toque la base de
/// datos no abre ninguna.
/// </summary>
internal sealed class DbSession(IOptions<DatabaseOptions> options) : IDbSession, IAsyncDisposable
{
    private NpgsqlConnection? _connection;

    public DbTransaction? Transaction { get; internal set; }

    public async Task<DbConnection> GetConnectionAsync(CancellationToken ct = default)
    {
        if (_connection is not null)
            return _connection;

        _connection = new NpgsqlConnection(options.Value.ConnectionString);
        await _connection.OpenAsync(ct);

        return _connection;
    }

    public async ValueTask DisposeAsync()
    {
        if (Transaction is not null)
        {
            await Transaction.DisposeAsync();
            Transaction = null;
        }

        if (_connection is not null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }
    }
}
