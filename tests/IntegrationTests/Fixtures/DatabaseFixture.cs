using NovaFE.Infrastructure.Persistence.EfCore;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Respawn;
using Testcontainers.PostgreSql;

namespace NovaFE.IntegrationTests.Fixtures;

/// <summary>
/// Levanta un PostgreSQL real en un contenedor, le aplica las migraciones de EF
/// Core y permite dejar la base limpia entre pruebas.
/// <para>
/// Se comparte por toda la colección: el contenedor arranca una sola vez
/// (es lo caro) y cada prueba solo paga el reseteo de datos, que es rápido.
/// </para>
/// </summary>
public sealed class DatabaseFixture : IAsyncLifetime
{
    private const string Image = "postgres:16";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder(Image)
        .WithCleanUp(true)
        .Build();

    private Respawner? _respawner;

    /// <summary>Cadena de conexión como el rol dueño (superusuario del contenedor): DDL, migraciones, seed.</summary>
    public string ConnectionString { get; private set; } = string.Empty;

    /// <summary>
    /// Cadena de conexión como un rol <b>restringido</b> (sin superusuario ni
    /// <c>BYPASSRLS</c>), igual que <c>novafe_app</c> en producción. Es lo que
    /// hace que las políticas RLS realmente se apliquen — ver
    /// <c>RowLevelSecurityTests</c> y <c>docs/multi-tenancy.md</c>.
    /// </summary>
    public string RestrictedConnectionString { get; private set; } = string.Empty;

    internal const string RestrictedRole = "novafe_rls_test";
    private const string RestrictedPassword = "rls_test_pw";

    public async ValueTask InitializeAsync()
    {
        if (!DockerAvailability.IsAvailable)
            return;

        await _container.StartAsync();

        ConnectionString = _container.GetConnectionString();

        await ApplyMigrationsAsync();
        await CreateRestrictedRoleAsync();

        // Respawn arma su plan de borrado leyendo el esquema, así que se crea
        // después de aplicar las migraciones.
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["public"],
            TablesToIgnore = ["__EFMigrationsHistory"],
        });
    }

    /// <summary>
    /// Borra todos los datos dejando el esquema intacto. Se llama antes de cada
    /// prueba para que ninguna dependa del estado que dejó la anterior.
    /// </summary>
    public async Task ResetAsync()
    {
        if (_respawner is null)
            return;

        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        await _respawner.ResetAsync(connection);
    }

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();

    private async Task ApplyMigrationsAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        await using var context = new AppDbContext(options, NullCurrentTenant.Instance);
        await context.Database.MigrateAsync();
    }

    /// <summary>
    /// Crea el rol restringido de runtime (los mismos privilegios que
    /// <c>deploy/sql/001-app-role.sql</c>: acceso a datos, nunca DDL, sin
    /// <c>BYPASSRLS</c>) y arma <see cref="RestrictedConnectionString"/>.
    /// </summary>
    private async Task CreateRestrictedRoleAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        var db = new NpgsqlConnectionStringBuilder(ConnectionString).Database;

        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = $"""
                DROP ROLE IF EXISTS {RestrictedRole};
                CREATE ROLE {RestrictedRole} LOGIN PASSWORD '{RestrictedPassword}'
                    NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS;
                GRANT CONNECT ON DATABASE "{db}" TO {RestrictedRole};
                GRANT USAGE ON SCHEMA public TO {RestrictedRole};
                GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO {RestrictedRole};
                GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO {RestrictedRole};
                """;
            await cmd.ExecuteNonQueryAsync();
        }

        RestrictedConnectionString = new NpgsqlConnectionStringBuilder(ConnectionString)
        {
            Username = RestrictedRole,
            Password = RestrictedPassword,
        }.ConnectionString;
    }
}
