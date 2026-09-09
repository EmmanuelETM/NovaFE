using NovaFE.Domain.Tenants;
using NovaFE.Service.Configuration;
using NovaFE.Service.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;

namespace NovaFE.Service.Extensions;

internal static class SecurityExtensions
{
    /// <summary>
    /// Autenticación y autorización de la API:
    /// <list type="bullet">
    /// <item>esquema <c>ApiKey</c> — clientes, header <c>X-API-Key</c>;</item>
    /// <item>esquema <c>InternalKey</c> — humanos del dashboard vía el BFF
    /// (<c>X-Internal-Key</c> + <c>X-Acting-User</c>/<c>X-Acting-Email</c>);</item>
    /// <item>esquema <c>DevTenantHeader</c> — <b>solo Development</b>, header <c>X-Tenant-Id</c> sin credencial;</item>
    /// <item>esquema <c>AdminKey</c> — operador, header <c>X-Admin-Key</c> contra <c>Security:AdminApiKey</c>.</item>
    /// </list>
    /// Políticas de contribuyente (RF-14.5, RBAC): <c>TenantConfig</c>,
    /// <c>EcfIssue</c>, <c>EcfRead</c> — cada una exige tenant + uno de los roles
    /// de <see cref="ApiKeyRole"/>. Más <c>Operator</c> y <c>Authenticated</c>.
    /// </summary>
    internal static IServiceCollection AddApiSecurity(
        this IServiceCollection services,
        IHostEnvironment environment,
        IConfiguration configuration)
    {
        services.AddOptions<SecurityOptions>()
            .Bind(configuration.GetSection(SecurityOptions.SectionName));

        services.AddSingleton<IApiKeyThrottle, InMemoryApiKeyThrottle>();

        var authentication = services
            .AddAuthentication(SecuritySchemes.ApiKey)
            .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(SecuritySchemes.ApiKey, null)
            .AddScheme<AuthenticationSchemeOptions, InternalKeyAuthenticationHandler>(SecuritySchemes.InternalKey, null)
            .AddScheme<AuthenticationSchemeOptions, AdminKeyAuthenticationHandler>(SecuritySchemes.AdminKey, null);

        // Máquina (API key) y humanos del dashboard (internal key) valen para las
        // políticas de contribuyente: ambos terminan en el claim tenant_id + rol.
        var tenantClientSchemes = new List<string> { SecuritySchemes.ApiKey, SecuritySchemes.InternalKey };

        // El atajo del header sin credencial solo existe en Development.
        if (environment.IsDevelopment())
        {
            authentication.AddScheme<AuthenticationSchemeOptions, DevTenantHeaderAuthenticationHandler>(
                SecuritySchemes.DevTenantHeader, null);
            tenantClientSchemes.Add(SecuritySchemes.DevTenantHeader);
        }

        void AddTenantPolicy(AuthorizationBuilder builder, string name, params string[] roles) =>
            builder.AddPolicy(name, policy =>
            {
                policy.AddAuthenticationSchemes([.. tenantClientSchemes]);
                policy.RequireClaim(SecuritySchemes.TenantClaim);
                policy.RequireRole(roles);
            });

        var authorization = services.AddAuthorizationBuilder();

        AddTenantPolicy(authorization, SecurityPolicies.TenantConfig, ApiKeyRole.AdminTenant.Name);
        AddTenantPolicy(authorization, SecurityPolicies.EcfIssue, ApiKeyRole.AdminTenant.Name, ApiKeyRole.Emisor.Name);
        AddTenantPolicy(
            authorization,
            SecurityPolicies.EcfRead,
            ApiKeyRole.AdminTenant.Name, ApiKeyRole.Emisor.Name, ApiKeyRole.Consultor.Name);

        authorization.AddPolicy(SecurityPolicies.Operator, policy =>
        {
            // La clave estática (rompe-cristal) o un humano operador vía el dashboard.
            policy.AddAuthenticationSchemes(SecuritySchemes.AdminKey, SecuritySchemes.InternalKey);
            policy.RequireRole("admin_sistema");
        });

        // Cualquier principal autenticado. Para GET /users/me. NO incluye AdminKey:
        // el rompe-cristal de operador no consume el dashboard, y en Development
        // ese handler autentica siempre (sin clave configurada) — se colaría como
        // identidad primaria de toda petición a /users/me.
        authorization.AddPolicy(SecurityPolicies.Authenticated, policy =>
        {
            policy.AddAuthenticationSchemes([.. tenantClientSchemes]);
            policy.RequireAuthenticatedUser();
        });

        return services;
    }
}
