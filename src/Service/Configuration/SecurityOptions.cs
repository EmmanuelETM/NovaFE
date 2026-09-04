namespace NovaFE.Service.Configuration;

/// <summary>
/// Ajustes de seguridad de la API. La autenticación de clientes va por API key
/// (tabla <c>api_keys</c>); esto es solo lo que se lee de configuración.
/// </summary>
public sealed class SecurityOptions
{
    public const string SectionName = "Security";

    /// <summary>
    /// Clave estática que protege los endpoints de operador (header
    /// <c>X-Admin-Key</c>). Vacía en Development = endpoints abiertos con un aviso;
    /// vacía fuera de Development = endpoints cerrados a cal y canto.
    /// </summary>
    public string? AdminApiKey { get; set; }
}
