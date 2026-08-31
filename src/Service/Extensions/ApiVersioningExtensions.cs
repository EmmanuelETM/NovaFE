using Asp.Versioning;
using Microsoft.AspNetCore.OpenApi;

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
    /// [ApiVersion("1")]
    /// [Route("api/v{version:apiVersion}/[controller]")]
    /// public class SolicitudesController : ApiController { }
    /// </code>
    /// </example>
    internal static IServiceCollection AddApiVersioningSetup(this IServiceCollection services)
    {
        services.AddApiVersioning(options =>
            {
                // Versionado solo por major: las URLs y las versiones declaradas son
                // "1", no "1.0". El minor se reserva para cambios compatibles que no
                // ameritan un segmento de URL nuevo.
                options.DefaultApiVersion = new ApiVersion(1);

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

        // El documento se llama "v1" (formato de grupo 'v'VVV). Le sumamos los
        // transformadores de esquema (ejemplos legibles del payload de emisión).
        services.Configure<OpenApiOptions>("v1", options =>
            options.AddSchemaTransformer<EcfOpenApiExamples>());

        return services;
    }
}
