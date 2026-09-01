using System.Net;
using System.Net.Http.Json;
using NovaFE.IntegrationTests.Fixtures;

namespace NovaFE.IntegrationTests.Dev;

/// <summary>
/// El endpoint de sandbox deja un contribuyente que puede emitir de inmediato.
/// </summary>
public sealed class DevSandboxTests(DatabaseFixture database) : IntegrationTestBase(database)
{
    [RequiresDockerFact]
    public async Task Sandbox_onboards_a_tenant_that_can_immediately_issue()
    {
        var setup = await Client.PostAsJsonAsync("/api/v1/dev/sandbox", new { });
        setup.StatusCode.ShouldBe(HttpStatusCode.OK, await setup.Content.ReadAsStringAsync());

        var sandbox = await LeerAsync<SandboxResponse>(setup);
        sandbox!.TenantId.ShouldNotBe(Guid.Empty);

        Client.DefaultRequestHeaders.Add("X-Tenant-Id", sandbox.TenantId.ToString());

        var post = await Client.PostAsJsonAsync("/api/v1/ecf", new
        {
            type = 31,
            incomeType = "01",
            buyer = new { name = "Cliente de Prueba SRL", rnc = "131880681" },
            payment = new { condition = "cash", methods = new[] { new { type = "cash", amount = 2360m } } },
            lines = new[] { new { name = "Consultoría", kind = "service", quantity = 1, unitPrice = 2000m, itbisRate = 1, unitOfMeasure = "43" } },
        });

        post.StatusCode.ShouldBe(HttpStatusCode.Created, await post.Content.ReadAsStringAsync());
        var issued = await LeerAsync<EcfResponse>(post);
        issued!.Encf.ShouldStartWith("E31");
        issued.Status.ShouldBe("signed");   // el fast-path está apagado en las pruebas
    }

    [RequiresDockerFact]
    public async Task Sandbox_certificate_endpoint_returns_a_pkcs12()
    {
        var response = await Client.GetAsync("/api/v1/dev/sandbox/certificate?rnc=130862346");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/x-pkcs12");
        (await response.Content.ReadAsByteArrayAsync()).Length.ShouldBeGreaterThan(500);
    }

    private sealed record SandboxResponse(Guid TenantId, string Rnc, string Environment);

    private sealed record EcfResponse(Guid Id, string Status, string Encf);
}
