using System.Net;
using NovaFE.IntegrationTests.Fixtures;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace NovaFE.IntegrationTests.Dgii;

public sealed class DgiiConnectionTests(DatabaseFixture database) : IntegrationTestBase(database)
{
    private const string SeedXml =
        "<SemillaModel><valor>0xggeol2rfxxmt22g4abxa91ycxblmyeci6h1</valor><fecha>2026-08-30T10:00:00-04:00</fecha></SemillaModel>";

    private const string SeedPath = "/testecf/autenticacion/api/autenticacion/semilla";
    private const string ValidatePath = "/testecf/autenticacion/api/autenticacion/validarsemilla";

    [RequiresDockerFact]
    public async Task Connection_reports_connected_when_dgii_issues_a_token()
    {
        using var dgii = new WireMockFixture();
        StubSeed(dgii, HttpStatusCode.OK);
        dgii.Server
            .Given(Request.Create().WithPath(ValidatePath).UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200).WithBodyAsJson(new
            {
                token = "dgii-token-xyz",
                expira = DateTimeOffset.UtcNow.AddHours(1),
                expedido = DateTimeOffset.UtcNow,
            }));

        Reconfigure(new Dictionary<string, string?> { ["Dgii:EcfBaseUrl"] = dgii.BaseUrl });

        await OnboardTenantWithCertificateAsync("130862346");

        var response = await Client.GetAsync("/api/v1.0/dgii/connection?environment=TestEcf");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var status = await LeerAsync<ConnectionStatus>(response);
        status!.Connected.ShouldBeTrue();
        status.Environment.ShouldBe("TestEcf");
        status.ExpiresAt.ShouldBeGreaterThan(DateTimeOffset.UtcNow);
    }

    [RequiresDockerFact]
    public async Task Connection_requests_the_seed_from_the_right_url_and_posts_the_signed_xml()
    {
        using var dgii = new WireMockFixture();
        StubSeed(dgii, HttpStatusCode.OK);
        dgii.Server
            .Given(Request.Create().WithPath(ValidatePath).UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200).WithBodyAsJson(new
            {
                token = "t",
                expira = DateTimeOffset.UtcNow.AddHours(1),
                expedido = DateTimeOffset.UtcNow,
            }));

        Reconfigure(new Dictionary<string, string?> { ["Dgii:EcfBaseUrl"] = dgii.BaseUrl });
        await OnboardTenantWithCertificateAsync("130111222");

        (await Client.GetAsync("/api/v1.0/dgii/connection?environment=TestEcf")).EnsureSuccessStatusCode();

        var requests = dgii.Server.LogEntries
            .Select(e => e.RequestMessage!)
            .Select(m => (Path: m.Path ?? string.Empty, Method: m.Method ?? string.Empty, Body: m.Body ?? string.Empty))
            .ToList();

        var seed = requests.Single(r => r.Path == SeedPath);
        seed.Method.ShouldBe("GET");

        var validate = requests.Single(r => r.Path == ValidatePath);
        validate.Method.ShouldBe("POST");
        validate.Body.ShouldContain("<Signature", Case.Sensitive);
    }

    [RequiresDockerFact]
    public async Task Connection_fails_when_dgii_rejects_the_seed()
    {
        using var dgii = new WireMockFixture();
        StubSeed(dgii, HttpStatusCode.ServiceUnavailable);

        Reconfigure(new Dictionary<string, string?> { ["Dgii:EcfBaseUrl"] = dgii.BaseUrl });
        await OnboardTenantWithCertificateAsync("130333444");

        var response = await Client.GetAsync("/api/v1.0/dgii/connection?environment=TestEcf");

        response.IsSuccessStatusCode.ShouldBeFalse();
    }

    [RequiresDockerFact]
    public async Task Connection_fails_when_the_tenant_has_no_certificate()
    {
        using var dgii = new WireMockFixture();
        StubSeed(dgii, HttpStatusCode.OK);

        Reconfigure(new Dictionary<string, string?> { ["Dgii:EcfBaseUrl"] = dgii.BaseUrl });
        await RegisterAndActAsTenantAsync("130555666"); // sin certificado

        var response = await Client.GetAsync("/api/v1.0/dgii/connection?environment=TestEcf");

        response.IsSuccessStatusCode.ShouldBeFalse();
    }

    private static void StubSeed(WireMockFixture dgii, HttpStatusCode status)
        => dgii.Server
            .Given(Request.Create().WithPath(SeedPath).UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode((int)status)
                .WithHeader("Content-Type", "application/xml")
                .WithBody(status == HttpStatusCode.OK ? SeedXml : string.Empty));

    private async Task OnboardTenantWithCertificateAsync(string rnc)
    {
        await RegisterAndActAsTenantAsync(rnc);
        (await Client.PostAsync("/api/v1.0/certificates",
            CertificateForm(TestPkcs12.Generate(holderIdentifier: rnc), TestPkcs12.DefaultPassword, "TestEcf")))
            .EnsureSuccessStatusCode();
    }

    private sealed record ConnectionStatus(
        bool Connected, string Environment, DateTimeOffset IssuedAt, DateTimeOffset ExpiresAt);
}
