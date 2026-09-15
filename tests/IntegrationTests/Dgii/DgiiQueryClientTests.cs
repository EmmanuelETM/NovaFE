using Microsoft.Extensions.DependencyInjection;
using NovaFE.Application.Dgii.Interfaces;
using NovaFE.Domain.Common;
using NovaFE.IntegrationTests.Fixtures;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace NovaFE.IntegrationTests.Dgii;

/// <summary>
/// <see cref="IDgiiQueryClient"/> (Módulo 10: trackIds y directorio) contra un
/// WireMock: URLs exactas, Bearer, parseo de las respuestas, y el guard de
/// CerteCF (§5.10 de la Descripción Técnica de Servicios DGII).
/// </summary>
public sealed class DgiiQueryClientTests(DatabaseFixture database) : IntegrationTestBase(database)
{
    private IDgiiQueryClient Resolve(out IServiceScope scope)
    {
        scope = Factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IDgiiQueryClient>();
    }

    [RequiresDockerFact]
    public async Task GetTrackIds_returns_every_entry_for_the_encf()
    {
        using var dgii = new WireMockFixture();
        dgii.Server
            .Given(Request.Create()
                .WithPath("/testecf/consultatrackids/api/trackids/consulta")
                .WithParam("rncemisor", "130000001")
                .WithParam("encf", "E310000000001")
                .UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBodyAsJson(new[]
            {
                new { trackId = "TRACK-1", estado = "Rechazado", fechaRecepcion = "2026-08-30T10:05:00-04:00" },
                new { trackId = "TRACK-2", estado = "Aceptado", fechaRecepcion = "2026-08-30T11:00:00-04:00" },
            }));

        Reconfigure(new Dictionary<string, string?> { ["Dgii:EcfBaseUrl"] = dgii.BaseUrl });

        var client = Resolve(out var scope);
        using (scope)
        {
            var result = await client.GetTrackIdsAsync(DgiiEnvironment.Test, "t", "130000001", "E310000000001");

            result.IsError.ShouldBeFalse(result.IsError ? result.FirstError.Description : "");
            result.Value.Count.ShouldBe(2);
            result.Value[0].TrackId.ShouldBe("TRACK-1");
            result.Value[1].Estado.ShouldBe("Aceptado");
        }

        var request = dgii.Server.LogEntries.Select(e => e.RequestMessage!).Single();
        request.Headers!["Authorization"].ToString().ShouldContain("Bearer t");
    }

    [RequiresDockerFact]
    public async Task GetTrackIds_in_certecf_is_rejected_without_calling_the_dgii()
    {
        using var dgii = new WireMockFixture();
        Reconfigure(new Dictionary<string, string?> { ["Dgii:EcfBaseUrl"] = dgii.BaseUrl });

        var client = Resolve(out var scope);
        using (scope)
        {
            var result = await client.GetTrackIdsAsync(DgiiEnvironment.Cert, "t", "130000001", "E310000000001");

            result.IsError.ShouldBeTrue();
            result.FirstError.Code.ShouldBe("Dgii.Query.NotAvailableInEnvironment");
        }

        dgii.Server.LogEntries.ShouldBeEmpty();
    }

    [RequiresDockerFact]
    public async Task ListDirectory_returns_the_full_list()
    {
        using var dgii = new WireMockFixture();
        dgii.Server
            .Given(Request.Create().WithPath("/testecf/consultadirectorio/api/consultas/listado").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBodyAsJson(new[]
            {
                new { rnc = "130000002", nombre = "Otro Contribuyente SRL", urlRecepcion = "https://otro.example/fe", urlAceptacion = (string?)null, urlOpcional = (string?)null },
            }));

        Reconfigure(new Dictionary<string, string?> { ["Dgii:EcfBaseUrl"] = dgii.BaseUrl });

        var client = Resolve(out var scope);
        using (scope)
        {
            var result = await client.ListDirectoryAsync(DgiiEnvironment.Test, "t");

            result.IsError.ShouldBeFalse(result.IsError ? result.FirstError.Description : "");
            result.Value.ShouldHaveSingleItem().Rnc.ShouldBe("130000002");
        }
    }

    [RequiresDockerFact]
    public async Task GetDirectoryEntry_not_found_is_null_not_an_error()
    {
        using var dgii = new WireMockFixture();
        dgii.Server
            .Given(Request.Create()
                .WithPath("/testecf/consultadirectorio/api/consultas/obtenerDirectorioporrnc")
                .WithParam("RNC", "999999999")
                .UsingGet())
            .RespondWith(Response.Create().WithStatusCode(404));

        Reconfigure(new Dictionary<string, string?> { ["Dgii:EcfBaseUrl"] = dgii.BaseUrl });

        var client = Resolve(out var scope);
        using (scope)
        {
            var result = await client.GetDirectoryEntryAsync(DgiiEnvironment.Test, "t", "999999999");

            result.IsError.ShouldBeFalse(result.IsError ? result.FirstError.Description : "");
            result.Value.ShouldBeNull();
        }
    }
}
