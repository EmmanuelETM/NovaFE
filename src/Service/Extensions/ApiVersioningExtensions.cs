using Asp.Versioning;

namespace NovaFE.Service.Extensions;

internal static class ApiVersioningExtensions
{
    /// <summary>
    /// Versionado por segmento de URL: <c>/api/v1/solicitudes</c>.
    /// <para>
    /// Viene configurado desde el día uno a propósito: agregar versionado después
    /// de tener endpoints en producción obliga a romper a todos los consumidores.
    /// </para>
    /// </summary>
    /// <example>
    /// <code>
    /// [ApiVersion("1.0")]
    /// [Route("api/v{version:apiVersion}/[controller]")]
    /// public class SolicitudesController : ApiController { }
    /// </code>
    /// </example>
    internal static IServiceCollection AddApiVersioningSetup(this IServiceCollection services)
    {
        services.AddApiVersioning(options =>
            {
                options.DefaultApiVersion = new ApiVersion(1, 0);

                // AssumeDefaultVersionWhenUnspecified se queda en false a propósito:
                // solo tiene sentido en APIs que ya estaban en producción antes de
                // tener versionado. En una API nueva, el cliente debe declarar qué
                // versión consume, y así agregar la v2 nunca rompe a nadie.

                // Devuelve las cabeceras api-supported-versions / api-deprecated-versions.
                options.ReportApiVersions = true;

                options.ApiVersionReader = new UrlSegmentApiVersionReader();
            })
            .AddMvc()
            .AddApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'VVV";
                options.SubstituteApiVersionInUrl = true;
            })
            // Genera un documento OpenAPI por versión de API. Reemplaza a
            // services.AddOpenApi(): esta variante conoce las versiones.
            .AddOpenApi();

        return services;
    }
}
