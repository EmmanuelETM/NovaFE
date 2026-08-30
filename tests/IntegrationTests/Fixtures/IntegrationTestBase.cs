using System.Net.Http.Json;
using System.Text.Json;
using NovaFE.Domain.Common.Json;

namespace NovaFE.IntegrationTests.Fixtures;

/// <summary>
/// Base para las pruebas de integración: expone un <see cref="HttpClient"/>
/// contra la API real y deja la base de datos limpia antes de cada prueba.
/// </summary>
[Collection(nameof(IntegrationTestCollection))]
public abstract class IntegrationTestBase(DatabaseFixture database) : IAsyncLifetime
{
    private ApiFactory? _factory;

    /// <summary>Contenedor de PostgreSQL compartido por la colección.</summary>
    protected DatabaseFixture Database { get; } = database;

    protected HttpClient Client { get; private set; } = null!;

    /// <summary>La fábrica de la app, para resolver servicios en pruebas que lo necesiten.</summary>
    protected ApiFactory Factory => _factory!;

    /// <summary>Las respuestas usan la misma configuración JSON que la API.</summary>
    protected static JsonSerializerOptions Json => JsonSettings.Bulletproof;

    public ValueTask InitializeAsync()
    {
        if (!DockerAvailability.IsAvailable)
            return ValueTask.CompletedTask;

        _factory = new ApiFactory(Database.ConnectionString);
        Client = _factory.CreateClient();

        // Cada prueba arranca con la base vacía: ninguna depende del orden.
        return new ValueTask(Database.ResetAsync());
    }

    public ValueTask DisposeAsync()
    {
        Client?.Dispose();
        _factory?.Dispose();

        GC.SuppressFinalize(this);

        return ValueTask.CompletedTask;
    }

    protected static Task<T?> LeerAsync<T>(HttpResponseMessage response)
    {
        ArgumentNullException.ThrowIfNull(response);

        return response.Content.ReadFromJsonAsync<T>(Json);
    }
}
