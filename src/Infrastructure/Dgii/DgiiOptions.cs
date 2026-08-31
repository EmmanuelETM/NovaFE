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

    /// <summary>
    /// Base del dominio de <b>Facturas de Consumo</b> (RFCE del tipo 32 &lt; DOP 250 k).
    /// Dominio distinto de <see cref="EcfBaseUrl"/>; el segmento de ambiente lo añade
    /// el cliente. Recepción RFCE en <c>{amb}/recepcionfc/api/recepcion/ecf</c>.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    [Url]
    public string FcBaseUrl { get; set; } = "https://fc.dgii.gov.do";

    /// <summary>Minutos antes del vencimiento para renovar el token (RF-01.3).</summary>
    [Range(1, 30)]
    public int TokenRenewalBufferMinutes { get; set; } = 5;

    /// <summary>Timeout total del cliente HTTP de autenticación.</summary>
    [Range(5, 180)]
    public int AuthTimeoutSeconds { get; set; } = 60;

    /// <summary>Timeout total de los clientes HTTP de envío y consulta a la DGII.</summary>
    [Range(5, 180)]
    public int SubmissionTimeoutSeconds { get; set; } = 60;

    public TimeSpan TokenRenewalBuffer => TimeSpan.FromMinutes(TokenRenewalBufferMinutes);
}
