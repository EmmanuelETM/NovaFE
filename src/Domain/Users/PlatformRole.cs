using NovaFE.Domain.Common;

namespace NovaFE.Domain.Users;

/// <summary>
/// Rol de un usuario humano de la plataforma (dashboard). <see cref="Enumeration{T}.Name"/>
/// usa el literal tal cual (<c>admin_sistema</c>/<c>admin_tenant</c>/<c>emisor</c>/
/// <c>consultor</c>) para que sea usable directo en las políticas de autorización,
/// igual que <see cref="Tenants.ApiKeyRole"/>.
/// <para>
/// A diferencia de <see cref="Tenants.ApiKeyRole"/> (que tiene solo 3, sin
/// <c>admin_sistema</c> — ninguna API key de máquina puede representar al operador),
/// acá sí está <c>admin_sistema</c>: es el rol del operador del SaaS, y un humano
/// operador se autentica por el mismo esquema que un humano de contribuyente.
/// </para>
/// </summary>
public sealed record PlatformRole(int Id, string Name) : Enumeration<PlatformRole>(Id, Name)
{
    /// <summary>Operador del SaaS. <c>TenantId</c> nulo.</summary>
    public static readonly PlatformRole AdminSistema = new(1, "admin_sistema");

    /// <summary>Administrador del contribuyente: configuración, certificados, secuencias, webhooks, usuarios.</summary>
    public static readonly PlatformRole AdminTenant = new(2, "admin_tenant");

    /// <summary>Emite, consulta lo propio, descarga la RI.</summary>
    public static readonly PlatformRole Emisor = new(3, "emisor");

    /// <summary>Solo lectura de comprobantes y estados.</summary>
    public static readonly PlatformRole Consultor = new(4, "consultor");

    /// <summary>Los roles que puede tener un usuario de contribuyente (todos menos <c>admin_sistema</c>).</summary>
    public bool IsTenantRole => this != AdminSistema;
}
