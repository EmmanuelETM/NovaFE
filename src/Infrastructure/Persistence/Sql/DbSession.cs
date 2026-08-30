using System.Data;
using System.Data.Common;
using Npgsql;
using NovaFE.Application.Common.Interfaces;
using Microsoft.Extensions.Options;

namespace NovaFE.Infrastructure.Persistence.Sql;

/// <summary>
/// Implementación de <see cref="IDbSession"/> con vida de scope (una por petición).
/// La conexión se abre de forma diferida: una petición que no toque la base de
/// datos no abre ninguna.
/// <para>
/// Al abrir, fija <c>app.tenant_id</c> igual que el interceptor de EF Core, para
/// que las lecturas Dapper de tablas con RLS también queden acotadas al tenant.
/// </para>
/// </summary>
internal sealed class DbSession(
    IOptions<DatabaseOptions> options,
    ICurrentTenant currentTenant) : IDbSession, IAsyncDisposable
{
    private NpgsqlConnection? _connection;

    public DbTransaction? Transaction { get; internal set; }

    public async Task<DbConnection> GetConnectionAsync(CancellationToken ct = default)
    {
        if (_connection is not null)
            return _connection;

        _connection = new NpgsqlConnection(options.Value.ConnectionString);
        await _connection.OpenAsync(ct);

        await using var command = _connection.CreateCommand();
        command.CommandText = "SELECT set_config('app.tenant_id', @tenant_id, false)";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "tenant_id";
        parameter.DbType = DbType.String;
        parameter.Value = currentTenant.TenantId?.ToString() ?? string.Empty;
        command.Parameters.Add(parameter);
        await command.ExecuteNonQueryAsync(ct);

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
