using NovaFE.Service.Common;

namespace NovaFE.Service.Middlewares;

/// <summary>
/// Resuelve el tenant de la petición y lo publica en <c>ICurrentTenant</c>.
/// <para>
/// Hoy lee el header <c>X-Tenant-Id</c> (un GUID). No bloquea las peticiones sin
/// tenant: los endpoints de operador (registrar contribuyentes, health, docs) no
/// lo necesitan, y los casos de uso que sí lo requieren fallan con un error
/// claro. La exigencia por ruta llegará junto con la autenticación por API key.
/// </para>
/// </summary>
internal sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Tenant-Id";

    public async Task InvokeAsync(HttpContext context, CurrentTenant currentTenant)
    {
        if (context.Request.Headers.TryGetValue(HeaderName, out var raw)
            && Guid.TryParse(raw, out var tenantId))
        {
            currentTenant.Set(tenantId);
        }

        await next(context);
    }
}
