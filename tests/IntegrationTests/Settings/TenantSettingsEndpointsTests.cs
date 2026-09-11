using System.Net;
using System.Net.Http.Json;
using NovaFE.IntegrationTests.Fixtures;

namespace NovaFE.IntegrationTests.Settings;

/// <summary>
/// <c>/api/v1/settings</c>: la config self-serve del contribuyente (política
/// <c>TenantConfig</c>; en pruebas, el header <c>X-Tenant-Id</c> → rol
/// <c>admin_tenant</c>).
/// </summary>
public sealed class TenantSettingsEndpointsTests(DatabaseFixture database) : IntegrationTestBase(database)
{
    private const string Key = "representation.default_layout";
    private const string Url = "/api/v1/settings";

    private sealed record SettingView(
        string Key, string ValueType, string DefaultValue, string EffectiveValue,
        bool IsOverridden, string ResolvedFrom, bool TenantWritable, string? UpdatedBy);

    private sealed record ChangeView(string? PreviousValue, string? NewValue, string? ChangedBy);

    private Task<HttpResponseMessage> PutAsync(string key, string value) =>
        Client.PutAsJsonAsync($"{Url}/{key}", new { value });

    [RequiresDockerFact]
    public async Task List_returns_the_tenant_definitions_with_their_default()
    {
        await RegisterAndActAsTenantAsync("130111222");

        var settings = await LeerAsync<List<SettingView>>(await Client.GetAsync(Url));

        var layout = settings!.ShouldHaveSingleItem();
        layout.Key.ShouldBe(Key);
        layout.EffectiveValue.ShouldBe("letter");
        layout.ResolvedFrom.ShouldBe("default");
        layout.IsOverridden.ShouldBeFalse();
        layout.TenantWritable.ShouldBeTrue();
    }

    [RequiresDockerFact]
    public async Task Put_overrides_canonicalizes_and_logs_the_change()
    {
        await RegisterAndActAsTenantAsync("130333444");

        var put = await PutAsync(Key, "POS");
        put.StatusCode.ShouldBe(HttpStatusCode.OK);

        var dto = await LeerAsync<SettingView>(put);
        dto!.EffectiveValue.ShouldBe("pos");
        dto.ResolvedFrom.ShouldBe("tenant");
        dto.IsOverridden.ShouldBeTrue();

        var history = await LeerAsync<List<ChangeView>>(await Client.GetAsync($"{Url}/{Key}/history"));
        var entry = history!.ShouldHaveSingleItem();
        entry.PreviousValue.ShouldBeNull();
        entry.NewValue.ShouldBe("pos");
        entry.ChangedBy.ShouldNotBeNull();
    }

    [RequiresDockerFact]
    public async Task Put_rejects_an_invalid_value_with_400()
    {
        await RegisterAndActAsTenantAsync("130555666");

        (await PutAsync(Key, "a4")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [RequiresDockerFact]
    public async Task Put_rejects_an_unknown_or_platform_key_with_404()
    {
        await RegisterAndActAsTenantAsync("130777888");

        (await PutAsync("nope.not.here", "pos")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await PutAsync("platform.maintenance_mode", "true")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [RequiresDockerFact]
    public async Task Delete_resets_to_the_default_and_records_it()
    {
        await RegisterAndActAsTenantAsync("130999000");

        await PutAsync(Key, "pos");
        (await Client.DeleteAsync($"{Url}/{Key}")).StatusCode.ShouldBe(HttpStatusCode.OK);

        var get = (await LeerAsync<List<SettingView>>(await Client.GetAsync(Url)))!.Single();
        get.EffectiveValue.ShouldBe("letter");
        get.ResolvedFrom.ShouldBe("default");

        var history = await LeerAsync<List<ChangeView>>(await Client.GetAsync($"{Url}/{Key}/history"));
        history!.Count.ShouldBe(2);
        history[0].NewValue.ShouldBeNull(); // el reset, más reciente
    }

    [RequiresDockerFact]
    public async Task Overrides_are_scoped_per_tenant()
    {
        var a = await RegisterTenantAsync("131111222");
        var b = await RegisterTenantAsync("131333444");

        ActAs(a);
        await PutAsync(Key, "pos");

        ActAs(b);
        var forB = (await LeerAsync<List<SettingView>>(await Client.GetAsync(Url)))!.Single();
        forB.EffectiveValue.ShouldBe("letter");
        forB.ResolvedFrom.ShouldBe("default");
    }

    [RequiresDockerFact]
    public async Task History_of_an_unknown_key_is_404()
    {
        await RegisterAndActAsTenantAsync("131555666");

        (await Client.GetAsync($"{Url}/nope.not.here/history")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
