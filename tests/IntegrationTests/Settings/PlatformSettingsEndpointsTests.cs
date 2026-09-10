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
        var put = await Client.PutAsJsonAsync($"{Url}/{Key}", new { value = "true" });
        put.StatusCode.ShouldBe(HttpStatusCode.OK);

        var dto = await LeerAsync<SettingView>(put);
        dto!.EffectiveValue.ShouldBe("true");
        dto.ResolvedFrom.ShouldBe("platform");
        dto.IsOverridden.ShouldBeTrue();

        var get = await LeerAsync<SettingView>(await Client.GetAsync($"{Url}/{Key}"));
        get!.EffectiveValue.ShouldBe("true");

        (await GenerationAsync()).ShouldBeGreaterThan(0);
        (await ChangeCountAsync()).ShouldBe(1);
    }

    [RequiresDockerFact]
    public async Task Put_canonicalizes_the_value()
    {
        await Client.PutAsJsonAsync($"{Url}/{Key}", new { value = "1" });

        var get = await LeerAsync<SettingView>(await Client.GetAsync($"{Url}/{Key}"));
        get!.EffectiveValue.ShouldBe("true");
    }

    [RequiresDockerFact]
    public async Task Put_rejects_an_invalid_value_with_400()
    {
        var put = await Client.PutAsJsonAsync($"{Url}/{Key}", new { value = "quizás" });

        put.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [RequiresDockerFact]
    public async Task Put_rejects_an_unknown_key_with_404()
    {
        var put = await Client.PutAsJsonAsync($"{Url}/nope.not.here", new { value = "true" });

        put.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [RequiresDockerFact]
    public async Task Delete_resets_to_the_default()
    {
        await Client.PutAsJsonAsync($"{Url}/{Key}", new { value = "true" });

        var delete = await Client.DeleteAsync($"{Url}/{Key}");
        delete.StatusCode.ShouldBe(HttpStatusCode.OK);

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
