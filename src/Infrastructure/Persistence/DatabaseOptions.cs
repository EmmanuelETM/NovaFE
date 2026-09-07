using System.ComponentModel.DataAnnotations;

namespace NovaFE.Infrastructure.Persistence;

/// <summary>
/// Configuración de acceso a datos. Se valida <b>al arrancar</b>: si falta el
/// connection string, la aplicación no levanta en lugar de fallar en el primer request.
/// </summary>
public sealed class DatabaseOptions
{
    /// <summary>Sección de configuración con los ajustes (timeouts, reintentos).</summary>
    public const string SectionName = "Database";

    /// <summary>Nombre del connection string dentro de <c>ConnectionStrings</c>.</summary>
    public const string ConnectionName = "Default";

    public const string ProviderPostgres = "postgres";
    public const string ProviderNeon = "neon";

    /// <summary>
    /// Proveedor del Postgres gestionado: <c>postgres</c> (por defecto, cualquier
    /// Postgres estándar) o <c>neon</c>. <c>neon</c> solo ajusta defaults —
    /// tiempo de conexión más alto (el compute de Neon se suspende y tiene cold
    /// start) y verificación TLS completa. No cambia el proveedor de Npgsql.
    /// Ver <c>docs/deployment.md</c>.
    /// </summary>
    public string Provider { get; set; } = ProviderPostgres;

    /// <summary>
    /// Tamaño máximo del pool de conexiones de Npgsql. Un Postgres gestionado
    /// (Neon, Azure) limita las conexiones; con una o dos réplicas un pool chico
    /// sobra. Se aplica al connection string si este no lo trae ya.
    /// </summary>
    [Range(1, 200)]
    public int MaxPoolSize { get; set; } = 20;

    /// <summary>
    /// Tiempo de espera para <b>abrir</b> una conexión (segundos). Con
    /// <c>Provider = neon</c> el default sube a 30: la primera conexión tras un
    /// rato de inactividad despierta el compute suspendido y tarda unos segundos.
    /// </summary>
    [Range(1, 300)]
    public int ConnectTimeoutSeconds { get; set; } = 15;

    /// <summary>
    /// Exige TLS (<c>SSL Mode = Require</c>). <c>false</c> por defecto para no
    /// romper el Postgres local / de Testcontainers, que no tiene TLS; en
    /// producción se activa en <c>appsettings.Production.json</c>. Con
    /// <c>Provider = neon</c> el TLS se fuerza igual (Neon siempre lo pide) y
    /// además se verifica la cadena del certificado.
    /// </summary>
    public bool RequireSsl { get; set; }

    /// <summary>
    /// Se toma de <c>ConnectionStrings:Default</c>, no de la sección Database:
    /// así sigue funcionando con las herramientas que esperan ese lugar estándar.
    /// </summary>
    [Required(AllowEmptyStrings = false,
        ErrorMessage = "Falta el connection string. Configúralo en ConnectionStrings:Default.")]
    public string ConnectionString { get; set; } = string.Empty;

    [Range(1, 600)]
    public int CommandTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Si es <c>true</c>, al arrancar el servicio aplica las migraciones pendientes
    /// y corre los <c>IDataSeeder</c> antes de aceptar tráfico. Cómodo en local;
    /// <b>false</b> por defecto. En producción con varias instancias, las
    /// migraciones deben ser un paso del despliegue, no del arranque.
    /// </summary>
    public bool MigrateOnStartup { get; set; }

    /// <summary>Reintentos ante fallos transitorios de PostgreSQL. 0 los desactiva.</summary>
    [Range(0, 10)]
    public int MaxRetryCount { get; set; } = 3;

    /// <summary>Solo en desarrollo: mensajes de error detallados de EF Core.</summary>
    public bool EnableDetailedErrors { get; set; }

    /// <summary>
    /// Solo en desarrollo: incluye los valores de los parámetros en los logs.
    /// Nunca lo enciendas en producción, expone datos personales en el log.
    /// </summary>
    public bool EnableSensitiveDataLogging { get; set; }
}
