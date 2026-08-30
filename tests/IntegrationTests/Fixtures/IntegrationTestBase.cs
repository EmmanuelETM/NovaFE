using System.Net.Http.Headers;
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
    private const string TenantHeader = "X-Tenant-Id";

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

    // --- helpers compartidos por los slices ---------------------------------

    /// <summary>Registra un contribuyente y devuelve su id.</summary>
    protected async Task<Guid> RegisterTenantAsync(string rnc, string plan = "Business")
    {
        Client.DefaultRequestHeaders.Remove(TenantHeader);

        var response = await Client.PostAsJsonAsync("/api/v1.0/tenants", new
        {
            rnc,
            legalName = $"Contribuyente {rnc}",
            plan,
        });

        response.EnsureSuccessStatusCode();
        return (await LeerAsync<IdResponse>(response))!.Id;
    }

    /// <summary>Hace que las peticiones siguientes vayan en nombre de este tenant.</summary>
    protected void ActAs(Guid tenantId)
    {
        Client.DefaultRequestHeaders.Remove(TenantHeader);
        Client.DefaultRequestHeaders.Add(TenantHeader, tenantId.ToString());
    }

    /// <summary>Registra un contribuyente y deja las peticiones actuando en su nombre.</summary>
    protected async Task<Guid> RegisterAndActAsTenantAsync(string rnc)
    {
        var id = await RegisterTenantAsync(rnc);
        ActAs(id);
        return id;
    }

    protected static MultipartFormDataContent CertificateForm(byte[] pkcs12, string password, string environment)
    {
        var file = new ByteArrayContent(pkcs12);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/x-pkcs12");

        return new MultipartFormDataContent
        {
            { file, "file", "certificate.p12" },
            { new StringContent(password), "password" },
            { new StringContent(environment), "environment" },
        };
    }

    protected sealed record IdResponse(Guid Id);
}
