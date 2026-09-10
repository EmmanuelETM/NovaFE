using System.Globalization;
using NovaFE.Application.Settings.Interfaces;
using NovaFE.Domain.Settings;
using Microsoft.AspNetCore.Mvc;

namespace NovaFE.Service.Middlewares;

/// <summary>
/// Cuando el setting <c>platform.maintenance_mode</c> está activo, responde
/// <c>503</c> a todo el tráfico salvo los health checks y la propia API de
/// settings — para poder apagarlo. Ver <c>docs/configuration.md</c>.
/// </summary>
internal sealed class MaintenanceModeMiddleware(RequestDelegate next)
{
    private const int RetryAfterSeconds = 120;

    private static readonly string[] AlwaysAllowed =
    [
        "/health",
        "/api/v1/platform-settings",
        "/openapi",
        "/scalar",
    ];

    public async Task InvokeAsync(HttpContext context, ISettingsReader settings, IProblemDetailsService problemDetails)
    {
        if (!settings.GetValue(SettingDefinitions.MaintenanceMode) || IsAllowed(context.Request.Path))
        {
            await next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        context.Response.Headers.RetryAfter = RetryAfterSeconds.ToString(CultureInfo.InvariantCulture);

        await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Servicio en mantenimiento",
                Detail = "La plataforma está en mantenimiento. Intenta de nuevo en unos minutos.",
            },
        });
    }

    private static bool IsAllowed(PathString path)
    {
        foreach (var prefix in AlwaysAllowed)
        {
            if (path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
