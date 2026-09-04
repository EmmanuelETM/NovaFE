using System.Security.Claims;
using NovaFE.Service.Common;
using NovaFE.Service.Security;

namespace NovaFE.Service.Middlewares;

/// <summary>
/// Pasa el tenant del principal autenticado a <c>ICurrentTenant</c>, que consumen
/// los casos de uso y la persistencia (filtro global de EF, RLS).
/// <para>
/// El contribuyente lo pone el esquema de autenticación en el claim
/// <c>tenant_id</c>: de la API key en producción, o del header <c>X-Tenant-Id</c>
/// en Development. Corre <b>después</b> de <c>UseAuthentication</c>. No bloquea las
/// peticiones sin tenant: la autorización (<c>[Authorize]</c>) es la que exige
/// credencial por ruta.
/// </para>
/// </summary>
internal sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, CurrentTenant currentTenant)
    {
        var raw = context.User.FindFirstValue(SecuritySchemes.TenantClaim);
        if (Guid.TryParse(raw, out var tenantId))
            currentTenant.Set(tenantId);

        await next(context);
    }
}
