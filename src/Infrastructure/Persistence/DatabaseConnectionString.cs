using Npgsql;

namespace NovaFE.Infrastructure.Persistence;

/// <summary>
/// Normaliza el connection string de PostgreSQL en un solo lugar, para que EF
/// Core, Dapper y los health checks consuman exactamente el mismo. Aplica el
/// tuning que quiere un Postgres gestionado y remoto (Neon, Azure): pool acotado,
/// tiempos de conexión y de comando explícitos, keepalive, TLS, y —clave para el
/// aislamiento multi-tenant— <c>No Reset On Close = false</c>, que hace que Npgsql
/// limpie <c>app.tenant_id</c> al devolver la conexión al pool.
/// <para>
/// Respeta los valores que el operador ya haya puesto en el string: solo rellena
/// las claves ausentes. Es idempotente.
/// </para>
/// </summary>
public static class DatabaseConnectionString
{
    /// <summary>Piso del connect timeout para Neon (cold start del compute suspendido).</summary>
    private const int NeonMinConnectTimeoutSeconds = 30;

    private const int KeepAliveSeconds = 30;

    public static string Normalize(string raw, DatabaseOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(raw);
        ArgumentNullException.ThrowIfNull(options);

        var isNeon = string.Equals(options.Provider?.Trim(), DatabaseOptions.ProviderNeon,
            StringComparison.OrdinalIgnoreCase);

        // Npgsql.ContainsKey responde "¿es una clave conocida?", no "¿la pusieron?".
        // Así que miramos las claves que trae el string crudo directamente,
        // normalizadas (minúsculas, sin espacios).
        var present = ProvidedKeys(raw);
        var builder = new NpgsqlConnectionStringBuilder(raw);

        if (!present.Contains("pooling"))
            builder.Pooling = true;

        if (!present.Contains("maximumpoolsize") && !present.Contains("maxpoolsize"))
            builder.MaxPoolSize = options.MaxPoolSize;

        if (!present.Contains("timeout"))
        {
            var connectTimeout = options.ConnectTimeoutSeconds;
            if (isNeon && connectTimeout < NeonMinConnectTimeoutSeconds)
                connectTimeout = NeonMinConnectTimeoutSeconds;
            builder.Timeout = connectTimeout;
        }

        if (!present.Contains("commandtimeout"))
            builder.CommandTimeout = options.CommandTimeoutSeconds;

        if (!present.Contains("keepalive"))
            builder.KeepAlive = KeepAliveSeconds;

        if (!present.Contains("sslmode"))
            builder.SslMode = isNeon
                ? SslMode.VerifyFull            // Neon siempre exige TLS
                : options.RequireSsl ? SslMode.Require : SslMode.Prefer;

        // El aislamiento por tenant depende de que una conexión reciclada no
        // arrastre el app.tenant_id de la petición anterior. Npgsql lo resetea al
        // devolverla al pool salvo que se desactive explícitamente; lo forzamos.
        if (!present.Contains("noresetonclose"))
            builder.NoResetOnClose = false;

        return builder.ConnectionString;
    }

    /// <summary>
    /// Heurística: ¿el host parece un pooler en modo <em>transaction</em>? Con uno
    /// así, <c>app.tenant_id</c> (variable de sesión) deja de aplicar sin avisar y
    /// RLS se rompe. Cubre el endpoint <c>-pooler</c> de Neon y PgBouncer. Ver
    /// <c>docs/multi-tenancy.md</c> §3.
    /// </summary>
    public static bool LooksLikeTransactionPooler(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return false;

        string host;
        try
        {
            host = new NpgsqlConnectionStringBuilder(connectionString).Host ?? string.Empty;
        }
        catch (ArgumentException)
        {
            return false;
        }

        return host.Contains("-pooler", StringComparison.OrdinalIgnoreCase)
               || host.Contains("pgbouncer", StringComparison.OrdinalIgnoreCase);
    }

    // Claves presentes en el string crudo, cada una en minúsculas y sin espacios
    // ("Command Timeout" → "commandtimeout"). Suficiente para las pocas claves que
    // nos interesan y sus alias con/sin espacio.
    private static HashSet<string> ProvidedKeys(string raw)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var pair in raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var eq = pair.IndexOf('=', StringComparison.Ordinal);
            if (eq <= 0)
                continue;

            var key = pair[..eq];
            var normalized = string.Concat(key.Where(c => !char.IsWhiteSpace(c))).ToLowerInvariant();
            if (normalized.Length > 0)
                keys.Add(normalized);
        }

        return keys;
    }
}
