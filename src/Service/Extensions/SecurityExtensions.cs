using NovaFE.Service.Configuration;
using NovaFE.Service.Security;
using Microsoft.AspNetCore.Authentication;

namespace NovaFE.Service.Extensions;

internal static class SecurityExtensions
{
    /// <summary>
    /// Autenticación y autorización de la API:
    /// <list type="bullet">
    /// <item>esquema <c>ApiKey</c> — clientes, header <c>X-API-Key</c>;</item>
    /// <item>esquema <c>DevTenantHeader</c> — <b>solo Development</b>, header <c>X-Tenant-Id</c> sin credencial;</item>
    /// <item>esquema <c>AdminKey</c> — operador, header <c>X-Admin-Key</c> contra <c>Security:AdminApiKey</c>.</item>
    /// </list>
    /// Políticas: <c>TenantClient</c> (recursos por contribuyente) y <c>Operator</c>.
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
            .AddScheme<AuthenticationSchemeOptions, AdminKeyAuthenticationHandler>(SecuritySchemes.AdminKey, null);

        // El atajo del header sin credencial solo existe en Development.
        var tenantClientSchemes = new List<string> { SecuritySchemes.ApiKey };
        if (environment.IsDevelopment())
        {
            authentication.AddScheme<AuthenticationSchemeOptions, DevTenantHeaderAuthenticationHandler>(
                SecuritySchemes.DevTenantHeader, null);
            tenantClientSchemes.Add(SecuritySchemes.DevTenantHeader);
        }

        services.AddAuthorizationBuilder()
            .AddPolicy(SecurityPolicies.TenantClient, policy =>
            {
                policy.AddAuthenticationSchemes([.. tenantClientSchemes]);
                policy.RequireClaim(SecuritySchemes.TenantClaim);
            })
            .AddPolicy(SecurityPolicies.Operator, policy =>
            {
                policy.AddAuthenticationSchemes(SecuritySchemes.AdminKey);
                policy.RequireAuthenticatedUser();
            });

        return services;
    }
}
