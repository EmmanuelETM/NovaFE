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

    public const string ApiKeyHeader = "X-API-Key";
    public const string TenantHeader = "X-Tenant-Id";
    public const string AdminKeyHeader = "X-Admin-Key";

    /// <summary>Claim que lleva el id del contribuyente en el principal autenticado.</summary>
    public const string TenantClaim = "tenant_id";
}

/// <summary>Nombres de políticas de autorización.</summary>
internal static class SecurityPolicies
{
    /// <summary>Recursos por contribuyente: API key (o, en Development, <c>X-Tenant-Id</c>).</summary>
    public const string TenantClient = "TenantClient";

    /// <summary>Recursos de operador del SaaS: admin key.</summary>
    public const string Operator = "Operator";
}
