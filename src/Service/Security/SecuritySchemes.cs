namespace NovaFE.Service.Security;

/// <summary>Nombres de esquemas de autenticación, políticas y headers de la API.</summary>
internal static class SecuritySchemes
{
    /// <summary>Autenticación de clientes por API key (<c>X-API-Key</c>).</summary>
    public const string ApiKey = "ApiKey";

    /// <summary>Solo Development: identifica el tenant por <c>X-Tenant-Id</c>, sin credencial.</summary>
    public const string DevTenantHeader = "DevTenantHeader";

    /// <summary>Endpoints de operador: clave estática <c>X-Admin-Key</c>.</summary>
    public const string AdminKey = "AdminKey";

    /// <summary>
    /// Humanos del dashboard: el BFF presenta <c>X-Internal-Key</c> + la identidad
    /// del humano en <c>X-Acting-User</c> / <c>X-Acting-Email</c>.
    /// </summary>
    public const string InternalKey = "InternalKey";

    public const string ApiKeyHeader = "X-API-Key";
    public const string TenantHeader = "X-Tenant-Id";
    public const string AdminKeyHeader = "X-Admin-Key";
    public const string InternalKeyHeader = "X-Internal-Key";
    public const string ActingUserHeader = "X-Acting-User";
    public const string ActingEmailHeader = "X-Acting-Email";

    /// <summary>Claim que lleva el id del contribuyente en el principal autenticado.</summary>
    public const string TenantClaim = "tenant_id";

    /// <summary>Claim con el ambiente de la DGII de la API key (<c>Test</c> / <c>Cert</c> / <c>Production</c>).</summary>
    public const string EnvironmentClaim = "dgii_environment";
}

/// <summary>
/// Nombres de políticas de autorización. Las de contribuyente exigen, además del
/// tenant, un rol de <c>ApiKeyRole</c> (RF-14.5) — el mapeo controller→política
/// sigue la tabla del Plan Técnico: <c>admin_tenant</c> es dueño de configuración/
/// certificados/secuencias, <c>emisor</c> puede emitir y consultar, <c>consultor</c>
/// solo consulta.
/// </summary>
internal static class SecurityPolicies
{
    /// <summary>Certificados, secuencias, conexión DGII — solo <c>admin_tenant</c>.</summary>
    public const string TenantConfig = "TenantConfig";

    /// <summary>Emitir e-CF / reencolar envío — <c>admin_tenant</c> o <c>emisor</c>.</summary>
    public const string EcfIssue = "EcfIssue";

    /// <summary>Consultar comprobantes y su estado — cualquiera de los 3 roles.</summary>
    public const string EcfRead = "EcfRead";

    /// <summary>Recursos de operador del SaaS: admin key o un humano <c>admin_sistema</c>.</summary>
    public const string Operator = "Operator";

    /// <summary>
    /// Cualquier principal autenticado, sin exigir rol ni tenant. Para
    /// <c>GET /users/me</c>, que responde según quién sea.
    /// </summary>
    public const string Authenticated = "Authenticated";
}
