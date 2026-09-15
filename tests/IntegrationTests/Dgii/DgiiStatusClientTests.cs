using Microsoft.Extensions.DependencyInjection;
using NovaFE.Application.Dgii.Interfaces;
using NovaFE.Domain.Common;
using NovaFE.IntegrationTests.Fixtures;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace NovaFE.IntegrationTests.Dgii;

/// <summary>
/// <see cref="IDgiiStatusClient"/> (Módulo 10: estatus de servicio) contra un
/// WireMock. El schema real de la respuesta no está documentado por la DGII —
/// estas pruebas cubren el parseo <b>tolerante</b> con JSON inventado en varias
/// formas, no un contrato verificado. Ver docs/dgii-queries.md.
/// </summary>
public sealed class DgiiStatusClientTests(DatabaseFixture database) : IntegrationTestBase(database)
{
    private IDgiiStatusClient Resolve(out IServiceScope scope)
    {
        scope = Factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IDgiiStatusClient>();
    }

    private void ReconfigureWithKey(string baseUrl, string apiKey = "test-key-123")
        => Reconfigure(new Dictionary<string, string?>
        {
            ["Dgii:StatusBaseUrl"] = baseUrl,
            ["Dgii:StatusApiKey"] = apiKey,
        });

    [RequiresDockerFact]
    public async Task GetServiceStatus_parses_a_bare_array_and_keeps_the_raw_json()
    {
        using var dgii = new WireMockFixture();
        dgii.Server
            .Given(Request.Create().WithPath("/api/EstatusServicios/ObtenerEstatus").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBodyAsJson(new[]
            {
                new { nombre = "Recepcion", disponible = true },
                new { nombre = "ConsultaResultado", disponible = false },
            }));

        ReconfigureWithKey(dgii.BaseUrl);

        var client = Resolve(out var scope);
        using (scope)
        {
            var result = await client.GetServiceStatusAsync();

            result.IsError.ShouldBeFalse(result.IsError ? result.FirstError.Description : "");
            result.Value.Count.ShouldBe(2);
            result.Value[0].Nombre.ShouldBe("Recepcion");
            result.Value[0].Disponible.ShouldBe(true);
            result.Value[0].RawJson.ShouldContain("Recepcion");
            result.Value[1].Disponible.ShouldBe(false);
        }

        var request = dgii.Server.LogEntries.Select(e => e.RequestMessage!).Single();
        request.Headers!["Authorization"].ToString().ShouldContain("ApiKey test-key-123");
    }

    [RequiresDockerFact]
    public async Task GetServiceStatus_finds_an_array_wrapped_in_an_object()
    {
        using var dgii = new WireMockFixture();
        dgii.Server
            .Given(Request.Create().WithPath("/api/EstatusServicios/ObtenerEstatus").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBodyAsJson(new
            {
                items = new[] { new { nombre = "Recepcion", disponible = true } },
            }));

        ReconfigureWithKey(dgii.BaseUrl);

        var client = Resolve(out var scope);
        using (scope)
        {
            var result = await client.GetServiceStatusAsync();

            result.IsError.ShouldBeFalse(result.IsError ? result.FirstError.Description : "");
            result.Value.ShouldHaveSingleItem().Nombre.ShouldBe("Recepcion");
        }
    }

    [RequiresDockerFact]
    public async Task GetServiceStatus_with_unknown_fields_still_keeps_the_raw_json_without_throwing()
    {
        using var dgii = new WireMockFixture();
        dgii.Server
            .Given(Request.Create().WithPath("/api/EstatusServicios/ObtenerEstatus").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBodyAsJson(new[]
            {
                new { servicioDesconocido = "algo", codigoRaro = 42 },
            }));

        ReconfigureWithKey(dgii.BaseUrl);

        var client = Resolve(out var scope);
        using (scope)
        {
            var result = await client.GetServiceStatusAsync();

            result.IsError.ShouldBeFalse(result.IsError ? result.FirstError.Description : "");
            var entry = result.Value.ShouldHaveSingleItem();
            entry.Nombre.ShouldBeNull();
            entry.Disponible.ShouldBeNull();
            entry.RawJson.ShouldContain("servicioDesconocido");
        }
    }

    [RequiresDockerFact]
    public async Task VerifyEnvironmentStatus_sends_the_numeric_ambiente_matching_our_environment_id()
    {
        using var dgii = new WireMockFixture();
        dgii.Server
            .Given(Request.Create()
                .WithPath("/api/EstatusServicios/VerificarEstado")
                .WithParam("Ambiente", "1")
                .UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBodyAsJson(new { enMantenimiento = false }));

        ReconfigureWithKey(dgii.BaseUrl);

        var client = Resolve(out var scope);
        using (scope)
        {
            var result = await client.VerifyEnvironmentStatusAsync(DgiiEnvironment.Test);

            result.IsError.ShouldBeFalse(result.IsError ? result.FirstError.Description : "");
            result.Value.EnMantenimiento.ShouldBe(false);
        }
    }

    [RequiresDockerFact]
    public async Task Without_an_api_key_configured_nothing_is_called()
    {
        using var dgii = new WireMockFixture();
        Reconfigure(new Dictionary<string, string?>
        {
            ["Dgii:StatusBaseUrl"] = dgii.BaseUrl,
            ["Dgii:StatusApiKey"] = "",
        });

        var client = Resolve(out var scope);
        using (scope)
        {
            var result = await client.GetServiceStatusAsync();

            result.IsError.ShouldBeTrue();
            result.FirstError.Code.ShouldBe("Dgii.Query.StatusApiKeyNotConfigured");
        }

        dgii.Server.LogEntries.ShouldBeEmpty();
    }
}
