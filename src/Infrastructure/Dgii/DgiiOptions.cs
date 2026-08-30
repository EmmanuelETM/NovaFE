using System.ComponentModel.DataAnnotations;

namespace NovaFE.Infrastructure.Dgii;

/// <summary>
/// Configuración de los servicios de la DGII. Los defaults apuntan a producción;
/// las pruebas y los ambientes de certificación los sobreescriben.
/// </summary>
public sealed class DgiiOptions
{
    public const string SectionName = "Dgii";

    /// <summary>
    /// Base del dominio de e-CF (autenticación, recepción, consultas). El segmento
    /// de ambiente (<c>testecf</c> / <c>certecf</c> / <c>ecf</c>) lo añade el cliente.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    [Url]
    public string EcfBaseUrl { get; set; } = "https://ecf.dgii.gov.do";

    /// <summary>Minutos antes del vencimiento para renovar el token (RF-01.3).</summary>
    [Range(1, 30)]
    public int TokenRenewalBufferMinutes { get; set; } = 5;

    /// <summary>Timeout total del cliente HTTP de autenticación.</summary>
    [Range(5, 180)]
    public int AuthTimeoutSeconds { get; set; } = 60;

    public TimeSpan TokenRenewalBuffer => TimeSpan.FromMinutes(TokenRenewalBufferMinutes);
}
