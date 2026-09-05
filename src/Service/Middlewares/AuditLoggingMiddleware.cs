using System.Diagnostics;
using System.Security.Claims;
using NovaFE.Application.Common;
using NovaFE.Application.Common.Interfaces;
using NovaFE.Service.Security;
using Microsoft.AspNetCore.Authorization;

namespace NovaFE.Service.Middlewares;

/// <summary>
/// Registra una fila de auditoría (RF-14.4) por cada petición a un endpoint
/// <c>[Authorize]</c> — health/openapi/scalar y los <c>dev/**</c> anónimos quedan
/// afuera sin mantener una lista de rutas a mano.
/// <para>
/// Se registra <b>antes</b> de <c>UseAuthorization()</c>, no después: un
/// middleware corre su código posterior a <c>await next(context)</c> cuando
/// <i>todo</i> lo que sigue ya terminó, así que puesto aquí ve el
/// <c>StatusCode</c> final — incluyendo los <c>401</c>/<c>403</c> que hoy corta
/// <c>UseAuthorization()</c> y que nunca llegarían a un middleware registrado
/// después de ella. Por eso el tenant sale del claim directamente
/// (<c>context.User</c>, poblado por la autenticación) y <b>no</b> de
/// <see cref="ICurrentTenant"/>: <c>TenantResolutionMiddleware</c> corre después
/// de <c>UseAuthorization()</c> y en un <c>401</c>/<c>403</c> nunca llega a
/// ejecutarse, así que <c>ICurrentTenant.TenantId</c> se quedaría en null incluso
/// para una petición con una key válida a la que solo le faltó el rol.
/// </para>
/// </summary>
internal sealed class AuditLoggingMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IAuditLogWriter writer, TimeProvider timeProvider)
    {
        var stopwatch = Stopwatch.StartNew();

        await next(context);

        stopwatch.Stop();

        if (context.GetEndpoint()?.Metadata.GetMetadata<IAuthorizeData>() is null)
            return;

        var tenantId = Guid.TryParse(context.User.FindFirstValue(SecuritySchemes.TenantClaim), out var parsed)
            ? parsed
            : (Guid?)null;

        var entry = new AuditLogEntry(
            OccurredAt: timeProvider.GetUtcNow(),
            TenantId: tenantId,
            Actor: context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous",
            ActorRole: context.User.FindFirstValue(ClaimTypes.Role),
            IpAddress: context.Connection.RemoteIpAddress?.ToString(),
            HttpMethod: context.Request.Method,
            Path: context.Request.Path.Value ?? string.Empty,
            StatusCode: context.Response.StatusCode,
            Succeeded: context.Response.StatusCode < 400,
            TraceId: context.Items["TraceId"]?.ToString(),
            DurationMs: (int)stopwatch.ElapsedMilliseconds);

        // CancellationToken.None a propósito: si el cliente se desconectó justo al
        // terminar la petición, igual queremos que quede el registro.
        await writer.WriteAsync(entry, CancellationToken.None);
    }
}
