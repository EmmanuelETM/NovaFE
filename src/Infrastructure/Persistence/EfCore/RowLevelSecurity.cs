using Microsoft.EntityFrameworkCore.Migrations;

namespace NovaFE.Infrastructure.Persistence.EfCore;

/// <summary>
/// Helpers para activar Row-Level Security por tenant en una migración. Cada tabla
/// con datos de clientes (una entidad <c>ITenantOwned</c>) debe llamar a
/// <see cref="Enable"/> en su migración de creación.
/// <para>
/// La política filtra por <c>app.tenant_id</c>, la variable de sesión que fija
/// <c>TenantConnectionInterceptor</c> en cada apertura de conexión.
/// <c>FORCE ROW LEVEL SECURITY</c> hace que ni siquiera el dueño de la tabla se
/// salte la política — pero un superusuario sí la ignora (ver docs/multi-tenancy.md).
/// </para>
/// </summary>
public static class RowLevelSecurity
{
    public const string PolicyName = "tenant_isolation";

    /// <summary>
    /// Activa RLS sobre <paramref name="table"/> y crea la política de aislamiento
    /// por tenant. La tabla debe tener una columna <c>tenant_id uuid not null</c>.
    /// </summary>
    public static void Enable(MigrationBuilder migrationBuilder, string table, string? schema = null)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);
        ArgumentException.ThrowIfNullOrWhiteSpace(table);

        var qualified = schema is null ? Quote(table) : $"{Quote(schema)}.{Quote(table)}";
        const string predicate =
            "tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid";

        migrationBuilder.Sql($"ALTER TABLE {qualified} ENABLE ROW LEVEL SECURITY;");
        migrationBuilder.Sql($"ALTER TABLE {qualified} FORCE ROW LEVEL SECURITY;");
        migrationBuilder.Sql($"""
            CREATE POLICY {Quote(PolicyName)} ON {qualified}
                USING ({predicate})
                WITH CHECK ({predicate});
            """);
    }

    /// <summary>Revierte <see cref="Enable"/> (para el <c>Down</c> de la migración).</summary>
    public static void Disable(MigrationBuilder migrationBuilder, string table, string? schema = null)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);
        ArgumentException.ThrowIfNullOrWhiteSpace(table);

        var qualified = schema is null ? Quote(table) : $"{Quote(schema)}.{Quote(table)}";

        migrationBuilder.Sql($"DROP POLICY IF EXISTS {Quote(PolicyName)} ON {qualified};");
        migrationBuilder.Sql($"ALTER TABLE {qualified} NO FORCE ROW LEVEL SECURITY;");
        migrationBuilder.Sql($"ALTER TABLE {qualified} DISABLE ROW LEVEL SECURITY;");
    }

    private static string Quote(string identifier) => $"\"{identifier}\"";
}
