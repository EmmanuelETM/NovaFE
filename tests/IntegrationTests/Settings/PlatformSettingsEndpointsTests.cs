using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NovaFE.Application.Settings.Interfaces;
using NovaFE.Infrastructure.Persistence.EfCore;
using NovaFE.IntegrationTests.Fixtures;

namespace NovaFE.IntegrationTests.Settings;

public sealed class PlatformSettingsEndpointsTests(DatabaseFixture database) : IntegrationTestBase(database)
{
    private const string Key = "platform.maintenance_mode";
    private const string Url = "/api/v1/platform-settings";

    private sealed record SettingView(
        string Key, string Group, string ValueType, string DefaultValue,
        string EffectiveValue, bool IsOverridden, string ResolvedFrom, string? UpdatedBy);

    private sealed record ChangeView(
        string? PreviousValue, string? NewValue, DateTimeOffset ChangedAt, string? ChangedBy);

    // maintenance_mode es sensible: la operación normal confirma.
    private Task<HttpResponseMessage> PutAsync(string key, string value, bool confirm = true) =>
        Client.PutAsJsonAsync($"{Url}/{key}", new { value, confirm });

    private Task<HttpResponseMessage> DeleteAsync(string key, bool confirm = true) =>
        Client.DeleteAsync($"{Url}/{key}?confirm={confirm.ToString().ToLowerInvariant()}");

    [RequiresDockerFact]
    public async Task List_returns_every_definition_with_its_default()
    {
        var settings = await LeerAsync<List<SettingView>>(await Client.GetAsync(Url));

        settings.ShouldNotBeNull();
        var maintenance = settings.Single(s => s.Key == Key);
        maintenance.EffectiveValue.ShouldBe("false");
        maintenance.ResolvedFrom.ShouldBe("default");
        maintenance.IsOverridden.ShouldBeFalse();
    }

    [RequiresDockerFact]
    public async Task Put_overrides_bumps_generation_and_logs_the_change()
    {
        var put = await PutAsync(Key, "true");
        put.StatusCode.ShouldBe(HttpStatusCode.OK);

        var dto = await LeerAsync<SettingView>(put);
        dto!.EffectiveValue.ShouldBe("true");
        dto.ResolvedFrom.ShouldBe("platform");
        dto.IsOverridden.ShouldBeTrue();
        // El operador de pruebas entra por el rompe-cristal → "operator".
        dto.UpdatedBy.ShouldBe("operator");

        (await GenerationAsync()).ShouldBeGreaterThan(0);
        (await ChangeCountAsync()).ShouldBe(1);
    }

    [RequiresDockerFact]
    public async Task Put_canonicalizes_the_value()
    {
        await PutAsync(Key, "1");

        var get = await LeerAsync<SettingView>(await Client.GetAsync($"{Url}/{Key}"));
        get!.EffectiveValue.ShouldBe("true");
    }

    [RequiresDockerFact]
    public async Task Put_rejects_an_invalid_value_with_400()
    {
        (await PutAsync(Key, "quizás")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [RequiresDockerFact]
    public async Task Put_rejects_an_unknown_key_with_404()
    {
        (await PutAsync("nope.not.here", "true")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [RequiresDockerFact]
    public async Task A_sensitive_setting_needs_confirm_on_put_and_delete()
    {
        (await PutAsync(Key, "true", confirm: false)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        (await PutAsync(Key, "true")).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await DeleteAsync(Key, confirm: false)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await DeleteAsync(Key)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [RequiresDockerFact]
    public async Task History_returns_the_changes_newest_first()
    {
        await PutAsync(Key, "true");
        await DeleteAsync(Key);

        var history = await LeerAsync<List<ChangeView>>(await Client.GetAsync($"{Url}/{Key}/history"));

        history!.Count.ShouldBe(2);
        history[0].NewValue.ShouldBeNull();       // el reset, más reciente
        history[0].PreviousValue.ShouldBe("true");
        history[1].PreviousValue.ShouldBeNull();  // la primera sobrescritura
        history[1].NewValue.ShouldBe("true");
        history[0].ChangedBy.ShouldNotBeNull();
    }

    [RequiresDockerFact]
    public async Task History_of_an_unknown_key_is_404()
    {
        (await Client.GetAsync($"{Url}/nope.not.here/history")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [RequiresDockerFact]
    public async Task Delete_resets_to_the_default()
    {
        await PutAsync(Key, "true");

        (await DeleteAsync(Key)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var get = await LeerAsync<SettingView>(await Client.GetAsync($"{Url}/{Key}"));
        get!.EffectiveValue.ShouldBe("false");
        get.ResolvedFrom.ShouldBe("default");

        (await ChangeCountAsync()).ShouldBe(2); // override + reset
    }

    private async Task<long> GenerationAsync()
    {
        using var scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISettingsGenerationStore>().CurrentAsync();
    }

    private async Task<int> ChangeCountAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await context.PlatformSettingChanges.CountAsync(c => c.Key == Key);
    }
}
