using System.Net;
using System.Net.Http.Json;
using NovaFE.IntegrationTests.Fixtures;

namespace NovaFE.IntegrationTests.Ecf;

/// <summary>
/// El endpoint de preview del e-CF (solo Development). No toca base de datos, pero
/// va por la API real.
/// </summary>
public sealed class EcfPreviewEndpointsTests(DatabaseFixture database) : IntegrationTestBase(database)
{
    [RequiresDockerFact]
    public async Task Samples_lists_one_per_type()
    {
        var response = await Client.GetAsync("/api/v1.0/dev/ecf-preview/samples");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var samples = await LeerAsync<List<SampleRow>>(response);
        samples!.Count.ShouldBe(10);
        samples.ShouldContain(sample => sample.Slug == "exportacion");
    }

    [RequiresDockerFact]
    public async Task A_sample_returns_the_raw_xml_with_the_xsd_header()
    {
        var response = await Client.GetAsync("/api/v1.0/dev/ecf-preview/samples/compras?raw=true");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/xml");
        response.Headers.GetValues("X-Ecf-Xsd-Valid").ShouldBe(["true"]);

        var xml = await response.Content.ReadAsStringAsync();
        xml.ShouldContain("<TipoeCF>41</TipoeCF>");
        xml.ShouldContain("<Retencion>");
    }

    [RequiresDockerFact]
    public async Task Post_builds_the_xml_from_a_raw_body()
    {
        var response = await Client.PostAsJsonAsync("/api/v1.0/dev/ecf-preview", new
        {
            type = 31,
            lines = new[]
            {
                new { rate = 1, name = "Item gravado", unitPrice = 1000m },
                new { rate = 4, name = "Item exento", unitPrice = 500m },
            },
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await LeerAsync<PreviewResponse>(response);
        body!.XsdValid.ShouldBeTrue();
        body.Xml.ShouldContain("<MontoTotal>1680</MontoTotal>");   // 1000 + 180 ITBIS + 500 exento
    }

    [RequiresDockerFact]
    public async Task Post_rfce_returns_the_reduced_document()
    {
        var response = await Client.PostAsJsonAsync("/api/v1.0/dev/ecf-preview/rfce?raw=true", new
        {
            document = new
            {
                type = 32,
                sequenceExpiresOn = (DateOnly?)null,
                buyer = new { name = "Consumidor Final", rnc = (string?)null },
                lines = new[] { new { unitPrice = 800m } },
            },
            securityCode = "aB3xZ9",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var xml = await response.Content.ReadAsStringAsync();
        xml.ShouldContain("<RFCE>");
        xml.ShouldContain("<CodigoSeguridadeCF>aB3xZ9</CodigoSeguridadeCF>");
        xml.ShouldNotContain("<DetallesItems>");
    }

    [RequiresDockerFact]
    public async Task An_unknown_type_is_a_400()
    {
        var response = await Client.PostAsJsonAsync("/api/v1.0/dev/ecf-preview", new { type = 99 });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private sealed record SampleRow(string Slug, string Title);

    private sealed record PreviewResponse(string Xml, bool XsdValid, string? XsdError);
}
