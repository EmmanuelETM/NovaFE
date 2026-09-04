using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using NovaFE.Application.Ecf.Interfaces;
using NovaFE.IntegrationTests.Fixtures;

namespace NovaFE.IntegrationTests.Ecf;

/// <summary>
/// El outbox de envío: <c>POST /ecf</c> deja una fila lista para el worker, y el
/// reclamo con <c>FOR UPDATE SKIP LOCKED</c> no la entrega dos veces.
/// </summary>
public sealed class EcfSubmissionOutboxTests(DatabaseFixture database) : IntegrationTestBase(database)
{
    private const string Rnc = "130862346";
    private static readonly string[] Phones = ["809-555-0100"];

    private async Task IssueOneAsync()
    {
        // Sin fast-path ni worker: la prueba inspecciona la fila cruda del outbox.
        Reconfigure(new Dictionary<string, string?>
        {
            ["EcfSubmission:Enabled"] = "false",
            ["EcfSubmission:SyncWaitBudgetSeconds"] = "0",
        });

        var tenantId = await RegisterAndActAsTenantAsync(Rnc);

        (await Client.PutAsJsonAsync($"/api/v1/tenants/{tenantId}/emitter-profile", new
        {
            address = "Av. 27 de Febrero 100",
            municipality = "010100",
            province = "010000",
            phones = Phones,
            email = "facturacion@almax.do",
            economicActivity = "Comercio",
            defaultEnvironment = "Test",
        })).EnsureSuccessStatusCode();

        (await Client.PostAsync("/api/v1/certificates",
            CertificateForm(TestPkcs12.Generate(holderIdentifier: Rnc), TestPkcs12.DefaultPassword, "Test")))
            .EnsureSuccessStatusCode();

        (await Client.PostAsJsonAsync("/api/v1/sequences", new
        {
            environment = "Test", type = 31, series = "E", rangeFrom = 1, rangeTo = 100,
        })).EnsureSuccessStatusCode();

        var post = await Client.PostAsJsonAsync("/api/v1/ecf", new
        {
            type = 31,
            incomeType = "01",
            buyer = new { name = "Mi Cliente SRL", rnc = "131880681" },
            payment = new { condition = "cash", methods = new[] { new { type = "cash", amount = 2360m } } },
            lines = new[] { new { name = "Consultoria", kind = "service", quantity = 1, unitPrice = 2000m, itbisRate = 1, unitOfMeasure = "43" } },
        });
        post.EnsureSuccessStatusCode();
    }

    [RequiresDockerFact]
    public async Task Issuing_enqueues_exactly_one_submit_row_that_a_claim_picks_up_once()
    {
        await IssueOneAsync();

        using var scope = Factory.Services.CreateScope();
        var queue = scope.ServiceProvider.GetRequiredService<IEcfSubmissionQueue>();

        var first = await queue.ClaimBatchAsync(10);
        first.Count.ShouldBe(1);
        first[0].Kind.ShouldBe(EcfSubmissionKind.Submit);
        first[0].Attempts.ShouldBe(0);
        first[0].TrackId.ShouldBeNull();

        // Ya está 'processing' → un segundo reclamo no la vuelve a entregar.
        (await queue.ClaimBatchAsync(10)).ShouldBeEmpty();
    }

    [RequiresDockerFact]
    public async Task A_stuck_processing_row_is_reaped_back_to_pending()
    {
        await IssueOneAsync();

        using var scope = Factory.Services.CreateScope();
        var queue = scope.ServiceProvider.GetRequiredService<IEcfSubmissionQueue>();

        var claimed = await queue.ClaimBatchAsync(10);
        claimed.Count.ShouldBe(1);

        // Nada vencido "hace mucho" todavía.
        (await queue.ReapStuckAsync(TimeSpan.FromMinutes(10))).ShouldBe(0);

        // Cualquier fila 'processing' cuenta como atascada con un umbral de 0.
        (await queue.ReapStuckAsync(TimeSpan.Zero)).ShouldBe(1);
        (await queue.ClaimBatchAsync(10)).Count.ShouldBe(1);
    }
}
