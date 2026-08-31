using System.Net;
using System.Text.Json;
using NovaFE.IntegrationTests.Fixtures;

namespace NovaFE.IntegrationTests.Ecf;

public sealed class EcfOpenApiTests(DatabaseFixture database) : IntegrationTestBase(database)
{
    [RequiresDockerFact]
    public async Task The_issue_ecf_body_has_a_readable_example()
    {
        var response = await Client.GetAsync("/openapi/v1.json");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var schema = doc.RootElement
            .GetProperty("components").GetProperty("schemas").GetProperty("IssueEcfCommand");

        // El default del tipo es 31 (no el 1 que Scalar pondría por su cuenta).
        schema.GetProperty("properties").GetProperty("type").GetProperty("default").GetInt32().ShouldBe(31);

        // Y hay un ejemplo con líneas y pago, no todos los campos en null.
        var example = schema.GetProperty("example");
        example.GetProperty("type").GetInt32().ShouldBe(31);
        example.GetProperty("lines").GetArrayLength().ShouldBe(1);
        example.GetProperty("payment").GetProperty("condition").GetString().ShouldBe("credit");
    }

    [RequiresDockerFact]
    public async Task All_paths_are_lowercase_kebab_case()
    {
        using var doc = JsonDocument.Parse(
            await (await Client.GetAsync("/openapi/v1.json")).Content.ReadAsStringAsync());

        var paths = doc.RootElement.GetProperty("paths").EnumerateObject().Select(p => p.Name).ToList();

        paths.ShouldContain("/api/v1/ecf");
        paths.ShouldContain("/api/v1/tenants/{id}/emitter-profile");
        paths.ShouldContain("/api/v1/dev/ecf-preview/samples");
        paths.ShouldAllBe(p => !p.Any(char.IsUpper));
    }
}
