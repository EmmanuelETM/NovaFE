using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace NovaFE.IntegrationTests.Fixtures;

/// <summary>
/// Levanta la API completa en memoria, apuntando a la base de datos del
/// contenedor. Es la aplicación real: mismo pipeline, mismos middlewares,
/// mismo contenedor de dependencias.
/// </summary>
public sealed class ApiFactory(
    string connectionString,
    IReadOnlyDictionary<string, string?>? overrides = null) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseEnvironment(Environments.Development);

        builder.ConfigureAppConfiguration((_, config) =>
        {
            var settings = new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = connectionString,

                // El esquema de las pruebas lo aplica DatabaseFixture; el arranque
                // de la app no debe migrar ni sembrar nada.
                ["Database:MigrateOnStartup"] = "false",

                // KEK de prueba (32 bytes base64) para el vault de certificados.
                ["CertificateVault:MasterKey"] = "gAqfTFC7NyyvGyBE+DgYdfbnJNv06yqDMmD3RFzUEwo=",

                // El rate limiting falsearía cualquier prueba que haga varias
                // peticiones seguidas.
                ["RateLimiting:Enabled"] = "false",

                // Sin colector de trazas en las pruebas.
                ["Observability:OtlpEndpoint"] = "",

                // Silencia el log de peticiones para que la salida de la suite
                // sea legible.
                ["Serilog:MinimumLevel:Default"] = "Warning",
            };

            if (overrides is not null)
            {
                foreach (var (key, value) in overrides)
                    settings[key] = value;
            }

            config.AddInMemoryCollection(settings);
        });
    }
}
