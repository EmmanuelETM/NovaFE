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

    public async ValueTask InitializeAsync()
    {
        if (!DockerAvailability.IsAvailable)
            return;

        // Cada prueba arranca con la base vacía: ninguna depende del orden. El
        // reseteo va ANTES de crear la fábrica porque el arranque de la app hace
        // un warm-load del snapshot de settings; si la base todavía trae los
        // overrides de la prueba anterior (p. ej. platform.maintenance_mode), la
        // instancia nueva nace en mantenimiento y responde 503 hasta que el
        // poller la corrige — una carrera que en CI se pierde.
        await Database.ResetAsync();

        _factory = new ApiFactory(Database.ConnectionString);
        Client = _factory.CreateClient();
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

    /// <summary>
    /// Reintenta <paramref name="condition"/> hasta que sea verdadera o se agoten
    /// los intentos. Para pruebas de workers eventualmente consistentes: no
    /// asumas que un solo tick del pump resuelve el trabajo. <paramref name="tick"/>
    /// (p. ej. disparar el pump) corre antes de cada chequeo.
    /// </summary>
    protected static async Task EventuallyAsync(
        Func<Task<bool>> condition,
        Func<Task>? tick = null,
        int attempts = 20,
        int delayMs = 100)
    {
        ArgumentNullException.ThrowIfNull(condition);

        for (var i = 0; i < attempts; i++)
        {
            if (tick is not null)
                await tick();

            if (await condition())
                return;

            await Task.Delay(delayMs);
        }

        throw new Shouldly.ShouldAssertException(
            $"La condición no se cumplió tras {attempts} intentos.");
    }

    /// <summary>
    /// Reconstruye la app con opciones de configuración extra (p. ej. apuntar la
    /// DGII a un WireMock). Llamar al inicio de la prueba, antes de escribir nada.
    /// </summary>
    protected void Reconfigure(IReadOnlyDictionary<string, string?> overrides)
    {
        Client.Dispose();
        _factory!.Dispose();

        _factory = new ApiFactory(Database.ConnectionString, overrides);
        Client = _factory.CreateClient();
    }

    // --- helpers compartidos por los slices ---------------------------------

    /// <summary>Registra un contribuyente y devuelve su id.</summary>
    protected async Task<Guid> RegisterTenantAsync(string rnc, string plan = "Business")
    {
        Client.DefaultRequestHeaders.Remove(TenantHeader);

        var response = await Client.PostAsJsonAsync("/api/v1/tenants", new
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
