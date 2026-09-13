using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using NovaFE.Application.Ecf.Submission;
using NovaFE.Application.Webhooks;
using NovaFE.IntegrationTests.Fixtures;

namespace NovaFE.IntegrationTests.Settings;

/// <summary>
/// Los ladders/tamaños de lote de envío a la DGII y de webhooks (antes hardcoded
/// o solo bootstrap, ver docs/configuration.md) ahora son settings runtime —
/// un `PUT` los refleja en la próxima construcción de
/// <see cref="EcfSubmissionSettings"/>/<see cref="WebhookSettings"/>, sin
/// reiniciar el proceso.
/// </summary>
public sealed class RuntimeRetrySettingsTests(DatabaseFixture database) : IntegrationTestBase(database)
{
    private const string Url = "/api/v1/platform-settings";

    // Ninguno de estos settings es sensible: sin `confirm`.
    private Task<HttpResponseMessage> PutAsync(string key, string value) =>
        Client.PutAsJsonAsync($"{Url}/{key}", new { value });

    [RequiresDockerFact]
    public async Task Overriding_the_poll_ladder_reflects_on_the_next_EcfSubmissionSettings()
    {
        (await PutAsync("submission.poll_ladder", "1m,2m")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await PutAsync("submission.backoff", "1m,3m")).StatusCode.ShouldBe(HttpStatusCode.OK);

        using var scope = Factory.Services.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<EcfSubmissionSettings>();

        settings.PollLadder.ShouldBe([TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2)]);
        settings.SubmitBackoff.ShouldBe([TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(3)]);
    }

    [RequiresDockerFact]
    public async Task Overriding_webhook_retry_settings_reflects_on_the_next_WebhookSettings()
    {
        (await PutAsync("webhooks.backoff_ladder", "5s,1m")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await PutAsync("webhooks.max_attempts", "3")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await PutAsync("webhooks.auto_disable_after_failures", "5")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await PutAsync("webhooks.delivery_timeout", "20s")).StatusCode.ShouldBe(HttpStatusCode.OK);

        using var scope = Factory.Services.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<WebhookSettings>();

        settings.BackoffLadder.ShouldBe([TimeSpan.FromSeconds(5), TimeSpan.FromMinutes(1)]);
        settings.MaxAttempts.ShouldBe(3);
        settings.AutoDisableAfterConsecutiveFailures.ShouldBe(5);
        settings.DeliveryTimeout.ShouldBe(TimeSpan.FromSeconds(20));
    }

    [RequiresDockerFact]
    public async Task A_malformed_ladder_is_rejected_and_the_previous_value_stays_in_effect()
    {
        (await PutAsync("submission.poll_ladder", "1m,2m")).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await PutAsync("submission.poll_ladder", "abc")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        using var scope = Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<EcfSubmissionSettings>()
            .PollLadder.ShouldBe([TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2)]);
    }
}
