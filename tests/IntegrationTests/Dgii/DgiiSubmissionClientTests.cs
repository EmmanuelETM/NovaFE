using System.Net;
using Microsoft.Extensions.DependencyInjection;
using NovaFE.Application.Dgii.Interfaces;
using NovaFE.Domain.Common;
using NovaFE.IntegrationTests.Fixtures;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace NovaFE.IntegrationTests.Dgii;

/// <summary>
/// El cliente de recepción/consulta contra un WireMock: URLs exactas, multipart,
/// Bearer, y el parseo de las tres respuestas de la DGII.
/// </summary>
public sealed class DgiiSubmissionClientTests(DatabaseFixture database) : IntegrationTestBase(database)
{
    private IDgiiSubmissionClient Resolve(out IServiceScope scope)
    {
        scope = Factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IDgiiSubmissionClient>();
    }

    [RequiresDockerFact]
    public async Task SubmitEcf_posts_the_signed_xml_and_returns_the_track_id()
    {
        using var dgii = new WireMockFixture();
        dgii.Server
            .Given(Request.Create().WithPath("/testecf/recepcion/api/facturaselectronicas").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200).WithBodyAsJson(new
            {
                trackId = "TRACK-ABC-123",
                error = "",
                mensaje = "Recibido",
            }));

        Reconfigure(new Dictionary<string, string?> { ["Dgii:EcfBaseUrl"] = dgii.BaseUrl });

        var client = Resolve(out var scope);
        using (scope)
        {
            var result = await client.SubmitEcfAsync(
                DgiiEnvironment.TestEcf, "bearer-xyz", "<ECF><x/></ECF>", "E310000000001");

            result.IsError.ShouldBeFalse(result.IsError ? result.FirstError.Description : "");
            result.Value.TrackId.ShouldBe("TRACK-ABC-123");
        }

        var request = dgii.Server.LogEntries.Select(e => e.RequestMessage!).Single();
        request.Path.ShouldBe("/testecf/recepcion/api/facturaselectronicas");
        request.Headers!["Authorization"].ToString().ShouldContain("Bearer bearer-xyz");
        (request.Body ?? string.Empty).ShouldContain("<ECF><x/></ECF>");
    }

    [RequiresDockerFact]
    public async Task SubmitEcf_without_a_track_id_is_a_gateway_error()
    {
        using var dgii = new WireMockFixture();
        dgii.Server
            .Given(Request.Create().WithPath("/testecf/recepcion/api/facturaselectronicas").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200).WithBodyAsJson(new
            {
                trackId = "",
                error = "XML_INVALIDO",
                mensaje = "El XML no cumple el XSD",
            }));

        Reconfigure(new Dictionary<string, string?> { ["Dgii:EcfBaseUrl"] = dgii.BaseUrl });

        var client = Resolve(out var scope);
        using (scope)
        {
            var result = await client.SubmitEcfAsync(
                DgiiEnvironment.TestEcf, "t", "<ECF/>", "E310000000001");

            result.IsError.ShouldBeTrue();
            result.FirstError.Code.ShouldBe("Dgii.Submission.NoTrackId");
        }
    }

    [RequiresDockerFact]
    public async Task SubmitEcf_maps_a_500_to_a_transport_error()
    {
        using var dgii = new WireMockFixture();
        dgii.Server
            .Given(Request.Create().WithPath("/testecf/recepcion/api/facturaselectronicas").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(500));

        Reconfigure(new Dictionary<string, string?> { ["Dgii:EcfBaseUrl"] = dgii.BaseUrl });

        var client = Resolve(out var scope);
        using (scope)
        {
            var result = await client.SubmitEcfAsync(
                DgiiEnvironment.TestEcf, "t", "<ECF/>", "E310000000001");

            result.IsError.ShouldBeTrue();
        }
    }

    [RequiresDockerFact]
    public async Task SubmitRfce_hits_the_fc_domain_and_returns_the_synchronous_result()
    {
        using var dgii = new WireMockFixture();
        dgii.Server
            .Given(Request.Create().WithPath("/testecf/recepcionfc/api/recepcion/ecf").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200).WithBodyAsJson(new
            {
                codigo = 1,
                estado = "Aceptado",
                mensajes = new[] { new { codigo = "0", valor = "OK" } },
                encf = "E320000000001",
                secuenciaUtilizada = true,
            }));

        Reconfigure(new Dictionary<string, string?> { ["Dgii:FcBaseUrl"] = dgii.BaseUrl });

        var client = Resolve(out var scope);
        using (scope)
        {
            var result = await client.SubmitRfceAsync(
                DgiiEnvironment.TestEcf, "t", "<RFCE/>", "E320000000001");

            result.IsError.ShouldBeFalse(result.IsError ? result.FirstError.Description : "");
            result.Value.Codigo.ShouldBe(1);
            result.Value.SecuenciaUtilizada.ShouldBe(true);
            result.Value.Mensajes[0].Value.ShouldBe("OK");
        }
    }

    [RequiresDockerFact]
    public async Task GetResult_parses_the_status_code_and_messages()
    {
        using var dgii = new WireMockFixture();
        dgii.Server
            .Given(Request.Create()
                .WithPath("/testecf/consultaresultado/api/consultas/estado")
                .WithParam("trackid", "TRACK-1")
                .UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBodyAsJson(new
            {
                trackId = "TRACK-1",
                codigo = 2,
                estado = "Rechazado",
                secuenciaUtilizada = false,
                mensajes = new[] { new { valor = "Firma inválida", codigo = 11 } },
            }));

        Reconfigure(new Dictionary<string, string?> { ["Dgii:EcfBaseUrl"] = dgii.BaseUrl });

        var client = Resolve(out var scope);
        using (scope)
        {
            var result = await client.GetResultAsync(DgiiEnvironment.TestEcf, "t", "TRACK-1");

            result.IsError.ShouldBeFalse(result.IsError ? result.FirstError.Description : "");
            result.Value.Codigo.ShouldBe(2);
            result.Value.SecuenciaUtilizada.ShouldBe(false);
            result.Value.Mensajes[0].Code.ShouldBe(11);
            result.Value.Mensajes[0].Value.ShouldBe("Firma inválida");
        }
    }
}
