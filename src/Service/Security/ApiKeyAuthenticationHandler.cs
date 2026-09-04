using System.Security.Claims;
using System.Text.Encodings.Web;
using NovaFE.Application.Tenants.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace NovaFE.Service.Security;

/// <summary>
/// Autentica una petición por el header <c>X-API-Key</c>. Resuelve el token a un
/// contribuyente vía <see cref="IApiKeyAuthenticator"/> y publica un principal con
/// el claim <c>tenant_id</c> (que luego <c>TenantResolutionMiddleware</c> pasa a
/// <c>ICurrentTenant</c>). Sin el header, no decide: deja pasar a otros esquemas.
/// </summary>
internal sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IApiKeyAuthenticator authenticator,
    IApiKeyThrottle throttle)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(SecuritySchemes.ApiKeyHeader, out var raw)
            || string.IsNullOrWhiteSpace(raw))
        {
            return AuthenticateResult.NoResult();
        }

        var client = Context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        if (throttle.IsBlocked(client))
            return AuthenticateResult.Fail("Demasiados intentos fallidos. Intenta de nuevo más tarde.");

        var identity = await authenticator.AuthenticateAsync(raw.ToString(), Context.RequestAborted);
        if (identity is null)
        {
            throttle.RegisterFailure(client);
            return AuthenticateResult.Fail("Credencial inválida.");
        }

        throttle.RegisterSuccess(client);

        var claims = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, $"apikey:{identity.KeyId}"),
                new Claim(ClaimTypes.Name, identity.TenantId.ToString()),
                new Claim(SecuritySchemes.TenantClaim, identity.TenantId.ToString()),
                new Claim(SecuritySchemes.EnvironmentClaim, identity.Environment),
            ],
            SecuritySchemes.ApiKey);

        var ticket = new AuthenticationTicket(new ClaimsPrincipal(claims), SecuritySchemes.ApiKey);
        return AuthenticateResult.Success(ticket);
    }
}
