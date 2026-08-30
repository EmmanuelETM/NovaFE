using Npgsql;
using Respawn;
using Testcontainers.PostgreSql;

namespace NovaFE.IntegrationTests.Fixtures;

/// <summary>
/// Levanta un PostgreSQL real en un contenedor, le aplica el esquema y permite
/// dejar la base limpia entre pruebas.
/// <para>
/// Se comparte por toda la colección: el contenedor arranca una sola vez
/// (es lo caro) y cada prueba solo paga el reseteo de datos, que es rápido.
/// </para>
/// </summary>
public sealed class DatabaseFixture : IAsyncLifetime
{
    private const string Imagen = "postgres:16";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder(Imagen)
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

        await AplicarEsquemaAsync();

        // Respawn arma su plan de borrado leyendo el esquema, así que se crea
        // después de aplicarlo.
        await using var conexion = new NpgsqlConnection(ConnectionString);
        await conexion.OpenAsync();

        _respawner = await Respawner.CreateAsync(conexion, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["public"],
            // Preserva la tabla de historial si más adelante usas migraciones.
            TablesToIgnore = ["__EFMigrationsHistory"]
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

        await using var conexion = new NpgsqlConnection(ConnectionString);
        await conexion.OpenAsync();

        await _respawner.ResetAsync(conexion);
    }

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();

    private async Task AplicarEsquemaAsync()
    {
        var ruta = Path.Combine(AppContext.BaseDirectory, "Fixtures", "schema.sql");

        if (!File.Exists(ruta))
            return;

        var script = await File.ReadAllTextAsync(ruta);

        if (string.IsNullOrWhiteSpace(script))
            return;

        await using var conexion = new NpgsqlConnection(ConnectionString);
        await conexion.OpenAsync();

        // Npgsql ejecuta varias sentencias separadas por ';' en un solo comando.
        await using var comando = new NpgsqlCommand(script, conexion);
        await comando.ExecuteNonQueryAsync();
    }
}
