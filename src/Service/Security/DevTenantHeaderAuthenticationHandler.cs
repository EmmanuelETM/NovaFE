using System.Security.Claims;
using System.Text.Encodings.Web;
using NovaFE.Domain.Tenants;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace NovaFE.Service.Security;

/// <summary>
/// <b>Solo Development.</b> Identifica el contribuyente por el header
/// <c>X-Tenant-Id</c> (un GUID), sin credencial — para el sandbox, el
/// <c>NovaFE.Service.http</c> y las pruebas de integración. En producción este
/// esquema no se registra y el único camino es la API key.
/// <para>
/// Publica el rol <see cref="ApiKeyRole.AdminTenant"/> (acceso total): es un
/// atajo de confianza que ya solo existe en Development, así que no tiene
/// sentido replicar el RBAC de las API keys reales aquí.
/// </para>
/// </summary>
internal sealed class DevTenantHeaderAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(SecuritySchemes.TenantHeader, out var raw)
            || !Guid.TryParse(raw, out var tenantId))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, $"devtenant:{tenantId}"),
                new Claim(ClaimTypes.Name, tenantId.ToString()),
                new Claim(SecuritySchemes.TenantClaim, tenantId.ToString()),
                new Claim(ClaimTypes.Role, ApiKeyRole.AdminTenant.Name),
            ],
            SecuritySchemes.DevTenantHeader);

        var ticket = new AuthenticationTicket(
            new ClaimsPrincipal(claims), SecuritySchemes.DevTenantHeader);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
