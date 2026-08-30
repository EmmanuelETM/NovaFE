using System.Reflection;
using Npgsql;
using NovaFE.Service.Configuration;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace NovaFE.Service.Extensions;

internal static class ObservabilityExtensions
{
    /// <summary>
    /// Trazas y métricas con OpenTelemetry, correlacionadas con el TraceId que
    /// propaga <c>TraceIdMiddleware</c>.
    /// <para>
    /// Los logs siguen yendo por Serilog. El exportador OTLP solo se registra si
    /// hay un endpoint configurado.
    /// </para>
    /// </summary>
    internal static IServiceCollection AddObservability(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<ObservabilityOptions>()
            .Bind(configuration.GetSection(ObservabilityOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var observability = configuration.GetSection(ObservabilityOptions.SectionName).Get<ObservabilityOptions>()
                            ?? new ObservabilityOptions();

        var exportar = !string.IsNullOrWhiteSpace(observability.OtlpEndpoint);
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(
                serviceName: observability.ServiceName,
                serviceVersion: version,
                serviceInstanceId: Environment.MachineName))
            .WithTracing(tracing =>
            {
                tracing.AddAspNetCoreInstrumentation(options =>
                    // El monitoreo golpea /health constantemente; no vale una traza cada vez.
                    options.Filter = context =>
                        !context.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase));

                tracing.AddHttpClientInstrumentation();
                tracing.AddNpgsql();

                if (exportar)
                    tracing.AddOtlpExporter(options => options.Endpoint = new Uri(observability.OtlpEndpoint!));
            })
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation();
                metrics.AddHttpClientInstrumentation();
                metrics.AddRuntimeInstrumentation();

                if (exportar)
                    metrics.AddOtlpExporter(options => options.Endpoint = new Uri(observability.OtlpEndpoint!));
            });

        return services;
    }
}
