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

    /// <summary>
    /// Se toma de <c>ConnectionStrings:Default</c>, no de la sección Database:
    /// así sigue funcionando con las herramientas que esperan ese lugar estándar.
    /// </summary>
    [Required(AllowEmptyStrings = false,
        ErrorMessage = "Falta el connection string. Configúralo en ConnectionStrings:Default.")]
    public string ConnectionString { get; set; } = string.Empty;

    [Range(1, 600)]
    public int CommandTimeoutSeconds { get; set; } = 30;

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
