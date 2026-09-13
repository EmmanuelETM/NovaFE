using ErrorOr;
using Microsoft.Extensions.DependencyInjection;
using NovaFE.Application.Dgii.Contracts;
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
/// una prueba, así que acá se overridean a valores chicos, respetando las
/// validaciones de la librería (`SamplingDuration ≥ 2×AttemptTimeout`,
/// `Retry:MaxRetryAttempts ≥ 1`).
/// </summary>
public sealed class DgiiCircuitBreakerTests(DatabaseFixture database) : IntegrationTestBase(database)
{
    private const string SubmitPath = "/testecf/recepcion/api/facturaselectronicas";

    private void ConfigureFastCircuitBreaker(WireMockFixture dgii) => Reconfigure(new Dictionary<string, string?>
    {
        ["Dgii:EcfBaseUrl"] = dgii.BaseUrl,
        // El mínimo de reintentos que admite la librería (0 no valida), sin
        // demora — así cada llamada sigue siendo rápida; cada llamada aporta
        // hasta 2 muestras al circuit breaker (intento + 1 reintento).
        ["Dgii:Resilience:Retry:MaxRetryAttempts"] = "1",
        ["Dgii:Resilience:Retry:Delay"] = "00:00:00",
        ["Dgii:Resilience:AttemptTimeout:Timeout"] = "00:00:01",
        ["Dgii:Resilience:CircuitBreaker:SamplingDuration"] = "00:00:05",
        ["Dgii:Resilience:CircuitBreaker:MinimumThroughput"] = "2",
        ["Dgii:Resilience:CircuitBreaker:FailureRatio"] = "0.5",
        ["Dgii:Resilience:CircuitBreaker:BreakDuration"] = "00:00:01",
    });

    private IDgiiSubmissionClient Resolve(out IServiceScope scope)
    {
        scope = Factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IDgiiSubmissionClient>();
    }

    private static Task<ErrorOr<DgiiSubmissionReceipt>> SubmitAsync(IDgiiSubmissionClient client) =>
        client.SubmitEcfAsync(DgiiEnvironment.Test, "t", "<ECF/>", "E310000000001");

    /// <summary>Llama hasta que el circuito abre (o se agotan los intentos). Con
    /// reintentos de por medio, no vale la pena predecir la llamada exacta.</summary>
    private static async Task<ErrorOr<DgiiSubmissionReceipt>> CallUntilCircuitOpensAsync(IDgiiSubmissionClient client)
    {
        for (var i = 0; i < 5; i++)
        {
            var result = await SubmitAsync(client);
            if (result.IsError && result.FirstError.Code == Errors.Http.CircuitOpen.Code)
                return result;
        }

        throw new InvalidOperationException("El circuito no abrió tras 5 llamadas.");
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
            (await CallUntilCircuitOpensAsync(client)).FirstError.Code.ShouldBe(Errors.Http.CircuitOpen.Code);

            var beforeNextCall = dgii.Server.LogEntries.Count;

            var result = await SubmitAsync(client);

            result.IsError.ShouldBeTrue();
            result.FirstError.Code.ShouldBe(Errors.Http.CircuitOpen.Code);
            // El circuito abierto ni siquiera intenta la llamada.
            dgii.Server.LogEntries.Count.ShouldBe(beforeNextCall);
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
            (await CallUntilCircuitOpensAsync(client)).FirstError.Code.ShouldBe(Errors.Http.CircuitOpen.Code);

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

            var result = await SubmitAsync(client);

            result.IsError.ShouldBeFalse(result.IsError ? result.FirstError.Description : "");
            result.Value.TrackId.ShouldBe("TRACK-RECOVERED");
        }
    }
}
