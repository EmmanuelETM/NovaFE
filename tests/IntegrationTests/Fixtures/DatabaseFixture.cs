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

    public string ConnectionString { get; private set; } = string.Empty;

    public async ValueTask InitializeAsync()
    {
        if (!DockerAvailability.IsAvailable)
            return;

        await _container.StartAsync();

        ConnectionString = _container.GetConnectionString();

        await ApplyMigrationsAsync();

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
}
