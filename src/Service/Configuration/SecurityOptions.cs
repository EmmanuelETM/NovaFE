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

    /// <summary>
    /// Secreto compartido con el BFF del dashboard (header <c>X-Internal-Key</c>).
    /// Autoriza al BFF a actuar en nombre de un humano ya autenticado por Better
    /// Auth, que se identifica en <c>X-Acting-User</c> / <c>X-Acting-Email</c>.
    /// Vacío = el esquema queda deshabilitado (ningún humano entra por esta vía).
    /// En producción va por Key Vault.
    /// </summary>
    public string? InternalApiKey { get; set; }
}
