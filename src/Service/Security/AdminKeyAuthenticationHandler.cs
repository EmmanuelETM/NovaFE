using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using NovaFE.Service.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace NovaFE.Service.Security;

/// <summary>
/// Protege los endpoints de operador con una clave estática de configuración
/// (<c>Security:AdminApiKey</c>, header <c>X-Admin-Key</c>). Comparación en tiempo
/// constante. Sin clave configurada: en Development autentica con un aviso; fuera
/// de Development rechaza todo.
/// </summary>
internal sealed class AdminKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IOptions<SecurityOptions> security,
    IHostEnvironment environment)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var configured = security.Value.AdminApiKey;

        if (string.IsNullOrWhiteSpace(configured))
        {
            if (environment.IsDevelopment())
            {
                Logger.LogWarning(
                    "Security:AdminApiKey no está configurada; los endpoints de operador quedan abiertos en Development.");
                return Task.FromResult(Grant());
            }

            return Task.FromResult(AuthenticateResult.Fail("El acceso de operador no está configurado."));
        }

        var presented = Request.Headers.TryGetValue(SecuritySchemes.AdminKeyHeader, out var raw)
            ? raw.ToString()
            : null;

        if (!string.IsNullOrEmpty(presented) && FixedTimeEquals(presented, configured))
            return Task.FromResult(Grant());

        return Task.FromResult(AuthenticateResult.Fail("Clave de operador inválida."));
    }

    private static bool FixedTimeEquals(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(a)),
            SHA256.HashData(Encoding.UTF8.GetBytes(b)));

    private static AuthenticateResult Grant()
    {
        var claims = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "operator"),
                new Claim(ClaimTypes.Name, "operator"),
                new Claim(ClaimTypes.Role, "admin_sistema"),
            ],
            SecuritySchemes.AdminKey);

        return AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(claims), SecuritySchemes.AdminKey));
    }
}
