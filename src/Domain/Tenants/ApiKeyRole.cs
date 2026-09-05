using NovaFE.Domain.Common;

namespace NovaFE.Domain.Tenants;

/// <summary>
/// Rol de una API key de contribuyente (RF-14.5). <see cref="Enumeration{T}.Name"/>
/// usa el literal del Plan Técnico tal cual (<c>admin_tenant</c>/<c>emisor</c>/
/// <c>consultor</c>) — mismo estilo que el claim <c>admin_sistema</c> ya fijo en
/// <c>AdminKeyAuthenticationHandler</c>, para que los valores de rol sean
/// consistentes en toda la API y usables directo en <c>RequireRole(...)</c>.
/// <para>
/// <c>admin_sistema</c> (el cuarto rol de RF-14.5) <b>no</b> vive aquí: es
/// exclusivo del operador del SaaS, que se autentica por un esquema distinto
/// (clave estática, no API key de tenant).
/// </para>
/// </summary>
public sealed record ApiKeyRole(int Id, string Name) : Enumeration<ApiKeyRole>(Id, Name)
{
    /// <summary>Configuración, certificados, secuencias, webhooks.</summary>
    public static readonly ApiKeyRole AdminTenant = new(1, "admin_tenant");

    /// <summary>Emitir, consultar propios, descargar RI.</summary>
    public static readonly ApiKeyRole Emisor = new(2, "emisor");

    /// <summary>Solo lectura de comprobantes y estados.</summary>
    public static readonly ApiKeyRole Consultor = new(3, "consultor");
}
