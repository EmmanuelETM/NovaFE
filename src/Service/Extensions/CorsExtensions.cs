using NovaFE.Service.Configuration;

namespace NovaFE.Service.Extensions;

internal static class CorsExtensions
{
    /// <summary>
    /// Registra la política CORS leída de configuración. Sin orígenes configurados,
    /// la política no autoriza nada: hay que declararlos explícitamente.
    /// </summary>
    internal static IServiceCollection AddCorsSetup(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<CorsOptions>()
            .Bind(configuration.GetSection(CorsOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var cors = configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>() ?? new CorsOptions();

        services.AddCors(options =>
        {
            options.AddPolicy(CorsOptions.PolicyName, policy =>
            {
                if (cors.AllowedOrigins.Length == 0)
                    return;

                policy.WithOrigins(cors.AllowedOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .WithExposedHeaders("X-Trace-Id");

                // AllowCredentials es incompatible con AllowAnyOrigin; por eso
                // los orígenes siempre se declaran uno por uno.
                if (cors.AllowCredentials)
                    policy.AllowCredentials();
            });
        });

        return services;
    }
}
