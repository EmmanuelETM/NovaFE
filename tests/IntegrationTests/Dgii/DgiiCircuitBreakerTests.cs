using Microsoft.Extensions.DependencyInjection;
using NovaFE.Application.Dgii.Interfaces;
using NovaFE.Domain.Common;
using NovaFE.IntegrationTests.Fixtures;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace NovaFE.IntegrationTests.Dgii;

/// <summary>
/// El circuit breaker de los clientes de la DGII (docs/dgii-submission.md
/// §Resiliencia): tras suficientes fallos abre y deja de golpear el servicio
/// caído; tras <c>BreakDuration</c> vuelve a intentar. Los defaults de
/// producción (`MinimumThroughput=5`, `SamplingDuration=30s`) son lentos para
/// una prueba, así que acá se overridean a valores chicos, respetando la
/// validación de la librería (`SamplingDuration ≥ 2×AttemptTimeout`).
/// </summary>
public sealed class DgiiCircuitBreakerTests(DatabaseFixture database) : IntegrationTestBase(database)
{
    private const string SubmitPath = "/testecf/recepcion/api/facturaselectronicas";

    private void ConfigureFastCircuitBreaker(WireMockFixture dgii) => Reconfigure(new Dictionary<string, string?>
    {
        ["Dgii:EcfBaseUrl"] = dgii.BaseUrl,
        // Sin reintentos: cada llamada es exactamente una muestra para el circuit
        // breaker (si no, el propio Retry —fuera de alcance de esta prueba— ya
        // generaría varias muestras y varios segundos de backoff dentro de una
        // sola llamada).
        ["Dgii:Resilience:Retry:MaxRetryAttempts"] = "0",
        ["Dgii:Resilience:AttemptTimeout:Timeout"] = "00:00:01",
        ["Dgii:Resilience:CircuitBreaker:SamplingDuration"] = "00:00:02",
        ["Dgii:Resilience:CircuitBreaker:MinimumThroughput"] = "2",
        ["Dgii:Resilience:CircuitBreaker:FailureRatio"] = "0.5",
        ["Dgii:Resilience:CircuitBreaker:BreakDuration"] = "00:00:01",
    });

    private IDgiiSubmissionClient Resolve(out IServiceScope scope)
    {
        scope = Factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IDgiiSubmissionClient>();
    }

    [RequiresDockerFact]
    public async Task Enough_failures_open_the_circuit_and_stop_hitting_the_server()
    {
        using var dgii = new WireMockFixture();
        dgii.Server
            .Given(Request.Create().WithPath(SubmitPath).UsingPost())
            .RespondWith(Response.Create().WithStatusCode(500));

        ConfigureFastCircuitBreaker(dgii);

        var client = Resolve(out var scope);
        using (scope)
        {
            // Dos fallos ya cumplen el mínimo de muestra (2) con 100% de fallos.
            await client.SubmitEcfAsync(DgiiEnvironment.Test, "t", "<ECF/>", "E310000000001");
            await client.SubmitEcfAsync(DgiiEnvironment.Test, "t", "<ECF/>", "E310000000001");

            var beforeThirdCall = dgii.Server.LogEntries.Count;

            var result = await client.SubmitEcfAsync(DgiiEnvironment.Test, "t", "<ECF/>", "E310000000001");

            result.IsError.ShouldBeTrue();
            result.FirstError.Code.ShouldBe(Errors.Http.CircuitOpen.Code);
            // El circuito abierto ni siquiera intenta la llamada.
            dgii.Server.LogEntries.Count.ShouldBe(beforeThirdCall);
        }
    }

    [RequiresDockerFact]
    public async Task The_circuit_closes_again_after_the_break_duration()
    {
        using var dgii = new WireMockFixture();
        dgii.Server
            .Given(Request.Create().WithPath(SubmitPath).UsingPost())
            .RespondWith(Response.Create().WithStatusCode(500));

        ConfigureFastCircuitBreaker(dgii);

        var client = Resolve(out var scope);
        using (scope)
        {
            await client.SubmitEcfAsync(DgiiEnvironment.Test, "t", "<ECF/>", "E310000000001");
            await client.SubmitEcfAsync(DgiiEnvironment.Test, "t", "<ECF/>", "E310000000001");

            (await client.SubmitEcfAsync(DgiiEnvironment.Test, "t", "<ECF/>", "E310000000001"))
                .FirstError.Code.ShouldBe(Errors.Http.CircuitOpen.Code);

            // Pasa el BreakDuration (1s) y el servicio se recupera.
            await Task.Delay(TimeSpan.FromSeconds(2));

            dgii.Server.Reset();
            dgii.Server
                .Given(Request.Create().WithPath(SubmitPath).UsingPost())
                .RespondWith(Response.Create().WithStatusCode(200).WithBodyAsJson(new
                {
                    trackId = "TRACK-RECOVERED",
                    error = "",
                    mensaje = "Recibido",
                }));

            var result = await client.SubmitEcfAsync(DgiiEnvironment.Test, "t", "<ECF/>", "E310000000001");

            result.IsError.ShouldBeFalse(result.IsError ? result.FirstError.Description : "");
            result.Value.TrackId.ShouldBe("TRACK-RECOVERED");
        }
    }
}
