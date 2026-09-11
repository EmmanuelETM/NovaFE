using Npgsql;
using NovaFE.IntegrationTests.Fixtures;

namespace NovaFE.IntegrationTests.Security;

/// <summary>
/// Row-Level Security ejercido de verdad: conectado como un rol <b>sin</b>
/// <c>BYPASSRLS</c> (igual que <c>novafe_app</c> en producción), no como el
/// superusuario del contenedor. Prueba que las políticas <c>tenant_isolation</c>
/// aíslan lecturas y escrituras entre contribuyentes. Ver
/// <c>docs/multi-tenancy.md</c>.
/// </summary>
public sealed class RowLevelSecurityTests(DatabaseFixture database) : IntegrationTestBase(database)
{
    private static readonly Guid TenantA = Guid.CreateVersion7();
    private static readonly Guid TenantB = Guid.CreateVersion7();

    private static async Task<NpgsqlConnection> OpenAsync(string connectionString, Guid? tenantId)
    {
        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT set_config('app.tenant_id', @t, false)";
        cmd.Parameters.AddWithValue("t", tenantId?.ToString() ?? string.Empty);
        await cmd.ExecuteNonQueryAsync();

        return connection;
    }

    private static async Task<long> CountEndpointsAsync(NpgsqlConnection connection)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT count(*) FROM webhook_endpoints";
        return (long)(await cmd.ExecuteScalarAsync())!;
    }

    private async Task SeedEndpointAsync(Guid tenantId, string url)
    {
        await using var connection = await OpenAsync(Database.ConnectionString, tenantId: null);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            INSERT INTO webhook_endpoints
                (id, tenant_id, url, secret, events, enabled, consecutive_failures, created_at, is_deleted)
            VALUES
                (gen_random_uuid(), @tid, @url, 's', ARRAY['ecf.accepted']::text[], true, 0, now(), false)
            """;
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("url", url);
        await cmd.ExecuteNonQueryAsync();
    }

    [RequiresDockerFact]
    public async Task The_test_role_is_not_exempt_from_rls()
    {
        await using var connection = await OpenAsync(Database.RestrictedConnectionString, tenantId: null);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            "SELECT rolsuper, rolbypassrls FROM pg_roles WHERE rolname = current_user";
        await using var r = await cmd.ExecuteReaderAsync();
        (await r.ReadAsync()).ShouldBeTrue();

        r.GetBoolean(0).ShouldBeFalse("el rol no debe ser superusuario");
        r.GetBoolean(1).ShouldBeFalse("el rol no debe tener BYPASSRLS");
    }

    [RequiresDockerFact]
    public async Task A_restricted_connection_only_sees_its_own_tenants_rows()
    {
        await SeedEndpointAsync(TenantA, "https://a.example.com/hook");
        await SeedEndpointAsync(TenantB, "https://b.example.com/hook");

        await using (var asA = await OpenAsync(Database.RestrictedConnectionString, TenantA))
            (await CountEndpointsAsync(asA)).ShouldBe(1);

        await using (var asB = await OpenAsync(Database.RestrictedConnectionString, TenantB))
            (await CountEndpointsAsync(asB)).ShouldBe(1);

        await using (var none = await OpenAsync(Database.RestrictedConnectionString, tenantId: null))
            (await CountEndpointsAsync(none)).ShouldBe(0);

        // El superusuario del contenedor las ve todas: la diferencia es el rol.
        await using (var asSuper = await OpenAsync(Database.ConnectionString, tenantId: null))
            (await CountEndpointsAsync(asSuper)).ShouldBe(2);
    }

    [RequiresDockerFact]
    public async Task The_with_check_policy_blocks_inserting_a_row_for_another_tenant()
    {
        await using var asA = await OpenAsync(Database.RestrictedConnectionString, TenantA);
        await using var cmd = asA.CreateCommand();
        cmd.CommandText =
            """
            INSERT INTO webhook_endpoints
                (id, tenant_id, url, secret, events, enabled, consecutive_failures, created_at, is_deleted)
            VALUES
                (gen_random_uuid(), @tid, 'https://x.example.com', 's', ARRAY['ecf.accepted']::text[], true, 0, now(), false)
            """;
        cmd.Parameters.AddWithValue("tid", TenantB);

        var ex = await Should.ThrowAsync<PostgresException>(() => cmd.ExecuteNonQueryAsync());
        ex.SqlState.ShouldBe("42501"); // insufficient_privilege — viola la política RLS
    }

    [RequiresDockerFact]
    public async Task Another_tenants_rows_are_invisible_to_update_and_delete()
    {
        await SeedEndpointAsync(TenantA, "https://a.example.com/hook");
        await SeedEndpointAsync(TenantB, "https://b.example.com/hook");

        await using var asA = await OpenAsync(Database.RestrictedConnectionString, TenantA);

        await using (var update = asA.CreateCommand())
        {
            update.CommandText = "UPDATE webhook_endpoints SET enabled = false WHERE tenant_id = @b";
            update.Parameters.AddWithValue("b", TenantB);
            (await update.ExecuteNonQueryAsync()).ShouldBe(0);
        }

        await using (var delete = asA.CreateCommand())
        {
            delete.CommandText = "DELETE FROM webhook_endpoints WHERE tenant_id = @b";
            delete.Parameters.AddWithValue("b", TenantB);
            (await delete.ExecuteNonQueryAsync()).ShouldBe(0);
        }

        // La fila de B sigue intacta (visto como superusuario).
        await using var asSuper = await OpenAsync(Database.ConnectionString, tenantId: null);
        await using var check = asSuper.CreateCommand();
        check.CommandText = "SELECT enabled FROM webhook_endpoints WHERE tenant_id = @b";
        check.Parameters.AddWithValue("b", TenantB);
        (await check.ExecuteScalarAsync()).ShouldBe(true);
    }

    [RequiresDockerFact]
    public async Task Tenant_settings_are_isolated_per_tenant()
    {
        await SeedTenantSettingAsync(TenantA, "pos");
        await SeedTenantSettingAsync(TenantB, "letter");

        await using (var asA = await OpenAsync(Database.RestrictedConnectionString, TenantA))
        {
            await using var read = asA.CreateCommand();
            read.CommandText = "SELECT count(*) FROM tenant_settings";
            (await read.ExecuteScalarAsync()).ShouldBe(1L);

            // WITH CHECK: no puede escribir una fila para otro tenant.
            await using var insert = asA.CreateCommand();
            insert.CommandText =
                """
                INSERT INTO tenant_settings (id, tenant_id, key, environment, value, created_at)
                VALUES (gen_random_uuid(), @b, 'representation.default_layout', '', 'pos', now())
                """;
            insert.Parameters.AddWithValue("b", TenantB);

            var ex = await Should.ThrowAsync<PostgresException>(() => insert.ExecuteNonQueryAsync());
            ex.SqlState.ShouldBe("42501");
        }
    }

    private async Task SeedTenantSettingAsync(Guid tenantId, string value)
    {
        await using var connection = await OpenAsync(Database.ConnectionString, tenantId: null);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            INSERT INTO tenant_settings (id, tenant_id, key, environment, value, created_at)
            VALUES (gen_random_uuid(), @tid, 'representation.default_layout', '', @value, now())
            """;
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("value", value);
        await cmd.ExecuteNonQueryAsync();
    }
}
