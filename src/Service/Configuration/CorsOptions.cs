namespace NovaFE.Service.Configuration;

/// <summary>
/// Orígenes autorizados a consumir la API desde un navegador.
/// <para>
/// Por defecto la lista está <b>vacía</b>, lo que bloquea todo el tráfico
/// cross-origin. Es intencional: abrir CORS debe ser una decisión explícita.
/// </para>
/// </summary>
public sealed class CorsOptions
{
    public const string SectionName = "Cors";

    /// <summary>Nombre de la política aplicada en el pipeline.</summary>
    public const string PolicyName = "Default";

    /// <summary>Orígenes completos, con esquema y puerto. Ej: https://app.novafe.do</summary>
    public string[] AllowedOrigins { get; set; } = [];

    /// <summary>Permite enviar cookies o cabeceras de autenticación. Requiere orígenes explícitos.</summary>
    public bool AllowCredentials { get; set; }
}
