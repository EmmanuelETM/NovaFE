using System.Data;
using System.Data.Common;
using NovaFE.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace NovaFE.Infrastructure.Persistence.EfCore.Interceptors;

/// <summary>
/// En cada apertura de conexión fija la variable de sesión <c>app.tenant_id</c>
/// que leen las políticas de Row-Level Security de PostgreSQL.
/// <para>
/// Si no hay tenant se fija a cadena vacía: así una conexión reutilizada del pool
/// nunca arrastra el tenant de la petición anterior. Npgsql ya resetea la sesión
/// al devolver la conexión al pool, pero fijarla en cada apertura es la garantía.
/// </para>
/// <para>
/// Nota: un rol superusuario de PostgreSQL ignora RLS. En local/pruebas la app se
/// conecta como <c>postgres</c>, así que ahí el aislamiento real lo da el filtro
/// global de consulta de EF Core; RLS es la defensa en producción, donde la app
/// se conecta con un rol restringido. Ver <c>docs/multi-tenancy.md</c>.
/// </para>
/// </summary>
public sealed class TenantConnectionInterceptor(ICurrentTenant currentTenant) : DbConnectionInterceptor
{
    private const string SetTenantSql = "SELECT set_config('app.tenant_id', @tenant_id, false)";

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        using var command = CreateSetTenantCommand(connection);
        command.ExecuteNonQuery();

        base.ConnectionOpened(connection, eventData);
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await using var command = CreateSetTenantCommand(connection);
        await command.ExecuteNonQueryAsync(cancellationToken);

        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
    }

    private DbCommand CreateSetTenantCommand(DbConnection connection)
    {
        var command = connection.CreateCommand();
        command.CommandText = SetTenantSql;

        var parameter = command.CreateParameter();
        parameter.ParameterName = "tenant_id";
        parameter.DbType = DbType.String;
        parameter.Value = currentTenant.TenantId?.ToString() ?? string.Empty;
        command.Parameters.Add(parameter);

        return command;
    }
}
