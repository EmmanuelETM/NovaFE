using System.Globalization;
using System.Threading.RateLimiting;
using NovaFE.Service.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace NovaFE.Service.Extensions;

internal static class RateLimitingExtensions
{
    /// <summary>
    /// Límite global por ventana fija, particionado por usuario autenticado o,
    /// si no hay, por IP. Los endpoints de salud quedan exentos para que el
    /// monitoreo no consuma la cuota ni reciba 429.
    /// </summary>
    internal static IServiceCollection AddRateLimitingSetup(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<RateLimitOptions>()
            .Bind(configuration.GetSection(RateLimitOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Se lee una sola vez al arrancar: cambiar los límites requiere reiniciar.
        var limits = configuration.GetSection(RateLimitOptions.SectionName).Get<RateLimitOptions>()
                     ?? new RateLimitOptions();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            {
                if (!limits.Enabled)
                    return RateLimitPartition.GetNoLimiter("disabled");

                if (httpContext.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase))
                    return RateLimitPartition.GetNoLimiter("health");

                var particion = httpContext.User.Identity?.IsAuthenticated == true
                    ? httpContext.User.Identity.Name ?? "authenticated"
                    : httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous";

                return RateLimitPartition.GetFixedWindowLimiter(particion, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = limits.PermitLimit,
                    Window = TimeSpan.FromSeconds(limits.WindowSeconds),
                    QueueLimit = limits.QueueLimit,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                });
            });

            // El rechazo responde en el mismo formato ProblemDetails que el resto
            // de los errores de la API, no con un 429 vacío.
            options.OnRejected = async (context, ct) =>
            {
                var httpContext = context.HttpContext;
                httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    httpContext.Response.Headers.RetryAfter =
                        ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                }

                var problemDetailsService = httpContext.RequestServices.GetRequiredService<IProblemDetailsService>();

                await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
                {
                    HttpContext = httpContext,
                    ProblemDetails = new ProblemDetails
                    {
                        Status = StatusCodes.Status429TooManyRequests,
                        Title = "Too Many Requests",
                        Detail = "Se excedió el límite de peticiones. Intenta de nuevo más tarde."
                    }
                });
            };
        });

        return services;
    }
}
