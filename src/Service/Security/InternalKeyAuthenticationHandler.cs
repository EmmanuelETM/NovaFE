using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using NovaFE.Application.Users.Interfaces;
using NovaFE.Domain.Users;
using NovaFE.Service.Configuration;

namespace NovaFE.Service.Security;

/// <summary>
/// Autentica a un humano del dashboard. El BFF de Next valida la sesión de Better
/// Auth y reenvía la identidad del humano en <c>X-Acting-User</c> /
/// <c>X-Acting-Email</c>, junto con <c>X-Internal-Key</c> — el secreto compartido
/// que autoriza al BFF a actuar en nombre de otro. Sin la internal key correcta,
/// las cabeceras <c>X-Acting-*</c> se ignoran (un cliente cualquiera no puede
/// suplantar a nadie).
/// <para>
/// Publica los mismos claims que <c>ApiKeyAuthenticationHandler</c>
/// (<c>tenant_id</c> + rol), así que las políticas de M14 no distinguen el origen.
/// El id del <see cref="Domain.Users.PlatformUser"/> va en
/// <c>NameIdentifier</c> como <c>user:{id}</c>.
/// </para>
/// <para>
/// Fase 5: si el actor ya autenticado es <c>admin_sistema</c> y trae
/// <c>X-Impersonate-User-Id</c>, los claims publicados son los del usuario
/// impersonado, no los del operador — más <c>impersonated_by</c> con el id
/// real. <c>ImpersonationReadOnlyFilter</c> usa ese claim para bloquear
/// escrituras; no es solo una anotación de auditoría.
/// </para>
/// </summary>
internal sealed class InternalKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IOptions<SecurityOptions> security,
    IPlatformUserAuthenticator authenticator)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(SecuritySchemes.InternalKeyHeader, out var presentedKey)
            || string.IsNullOrWhiteSpace(presentedKey))
        {
            return AuthenticateResult.NoResult();
        }

        var configured = security.Value.InternalApiKey;
        if (string.IsNullOrWhiteSpace(configured))
        {
            Logger.LogWarning(
                "Llegó una petición con {Header} pero Security:InternalApiKey no está configurada.",
                SecuritySchemes.InternalKeyHeader);
            return AuthenticateResult.Fail("El acceso del dashboard no está configurado.");
        }

        if (!FixedTimeEquals(presentedKey.ToString(), configured))
            return AuthenticateResult.Fail("Internal key inválida.");

        var actingUser = Header(SecuritySchemes.ActingUserHeader);
        var actingEmail = Header(SecuritySchemes.ActingEmailHeader);

        if (actingUser is null && actingEmail is null)
            return AuthenticateResult.Fail("Falta la identidad del usuario (X-Acting-User / X-Acting-Email).");

        Guid? actingTenantId = Guid.TryParse(Header(SecuritySchemes.ActingTenantHeader), out var parsedTenantId)
            ? parsedTenantId
            : null;

        var identity = await authenticator.AuthenticateAsync(
            actingUser, actingEmail ?? string.Empty, actingTenantId, Context.RequestAborted);

        if (identity is null)
            return AuthenticateResult.Fail("El usuario no está dado de alta, fue revocado, o no tiene acceso a ese tenant.");

        Guid? impersonatedBy = null;

        // Fase 5: un operador puede pedir ver la API como otro usuario, para
        // soporte — solo si el actor real ya autenticó como admin_sistema.
        if (string.Equals(identity.Role, PlatformRole.AdminSistema.Name, StringComparison.Ordinal)
            && Guid.TryParse(Header(SecuritySchemes.ImpersonateUserHeader), out var targetUserId))
        {
            var impersonated = await authenticator.ImpersonateAsync(targetUserId, actingTenantId, Context.RequestAborted);
            if (impersonated is null)
                return AuthenticateResult.Fail("El usuario a impersonar no existe, fue revocado, es otro operador, o no tiene acceso a ese tenant.");

            impersonatedBy = identity.UserId;
            identity = impersonated;
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, $"user:{identity.UserId}"),
            new(ClaimTypes.Name, identity.Email),
            new(ClaimTypes.Role, identity.Role),
        };

        if (identity.TenantId is { } tenantId)
            claims.Add(new Claim(SecuritySchemes.TenantClaim, tenantId.ToString()));

        if (impersonatedBy is { } operatorId)
            claims.Add(new Claim(SecuritySchemes.ImpersonatedByClaim, $"user:{operatorId}"));

        var ticket = new AuthenticationTicket(
            new ClaimsPrincipal(new ClaimsIdentity(claims, SecuritySchemes.InternalKey)),
            SecuritySchemes.InternalKey);

        return AuthenticateResult.Success(ticket);
    }

    private string? Header(string name) =>
        Request.Headers.TryGetValue(name, out var raw) && !string.IsNullOrWhiteSpace(raw)
            ? raw.ToString()
            : null;

    private static bool FixedTimeEquals(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(a)),
            SHA256.HashData(Encoding.UTF8.GetBytes(b)));
}
