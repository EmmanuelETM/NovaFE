using System.Net;
using System.Net.Http.Json;
using System.Text;
using NovaFE.IntegrationTests.Fixtures;

namespace NovaFE.IntegrationTests.Ecf;

/// <summary>
/// <c>GET /api/v1/ecf/{id}/representation</c> sirve la Representación Impresa en PDF
/// de un comprobante emitido.
/// </summary>
public sealed class EcfRepresentationEndpointTests(DatabaseFixture database) : IntegrationTestBase(database)
{
    private async Task<Guid> IssueOneAsync()
    {
        var setup = await Client.PostAsJsonAsync("/api/v1/dev/sandbox", new { });
        setup.StatusCode.ShouldBe(HttpStatusCode.OK, await setup.Content.ReadAsStringAsync());
        var sandbox = await LeerAsync<SandboxResponse>(setup);
        Client.DefaultRequestHeaders.Add("X-Tenant-Id", sandbox!.TenantId.ToString());

        var post = await Client.PostAsJsonAsync("/api/v1/ecf", new
        {
            type = 31,
            incomeType = "01",
            buyer = new { name = "Cliente de Prueba SRL", rnc = "131880681" },
            payment = new { condition = "cash", methods = new[] { new { type = "cash", amount = 2360m } } },
            lines = new[] { new { name = "Consultoría", kind = "service", quantity = 1, unitPrice = 2000m, itbisRate = 1, unitOfMeasure = "43" } },
        });
        post.StatusCode.ShouldBe(HttpStatusCode.Created, await post.Content.ReadAsStringAsync());
        return (await LeerAsync<EcfResponse>(post))!.Id;
    }

    [RequiresDockerFact]
    public async Task Serves_an_inline_pdf_and_a_download()
    {
        var id = await IssueOneAsync();

        var inline = await Client.GetAsync($"/api/v1/ecf/{id}/representation");
        inline.StatusCode.ShouldBe(HttpStatusCode.OK);
        inline.Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf");

        var bytes = await inline.Content.ReadAsByteArrayAsync();
        bytes.Length.ShouldBeGreaterThan(3000);
        Encoding.ASCII.GetString(bytes, 0, 5).ShouldBe("%PDF-");

        inline.Content.Headers.ContentDisposition!.DispositionType.ShouldBe("inline");
        (inline.Content.Headers.ContentDisposition.FileName ?? "").Trim('"').ShouldEndWith(".pdf");

        var download = await Client.GetAsync($"/api/v1/ecf/{id}/representation?download=true");
        download.Content.Headers.ContentDisposition!.DispositionType.ShouldBe("attachment");
    }

    [RequiresDockerFact]
    public async Task Unknown_comprobante_is_404()
    {
        await IssueOneAsync(); // deja un tenant en el header

        var response = await Client.GetAsync($"/api/v1/ecf/{Guid.NewGuid()}/representation");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [RequiresDockerFact]
    public async Task The_pos_layout_is_a_400_for_now()
    {
        var id = await IssueOneAsync();

        var response = await Client.GetAsync($"/api/v1/ecf/{id}/representation?layout=pos");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private sealed record SandboxResponse(Guid TenantId);

    private sealed record EcfResponse(Guid Id, string Encf);
}
