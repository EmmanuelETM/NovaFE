using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace NovaFE.Service.Security;

/// <summary>
/// <b>Solo Development.</b> Identifica el contribuyente por el header
/// <c>X-Tenant-Id</c> (un GUID), sin credencial — para el sandbox, el
/// <c>NovaFE.Service.http</c> y las pruebas de integración. En producción este
/// esquema no se registra y el único camino es la API key.
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
            ],
            SecuritySchemes.DevTenantHeader);

        var ticket = new AuthenticationTicket(
            new ClaimsPrincipal(claims), SecuritySchemes.DevTenantHeader);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
