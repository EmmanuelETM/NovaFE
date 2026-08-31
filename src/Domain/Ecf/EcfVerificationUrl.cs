using System.Globalization;
using System.Text;
using NovaFE.Domain.Common;

namespace NovaFE.Domain.Ecf;

/// <summary>
/// Construye la URL de verificación del <b>timbre QR</b> de la Representación
/// Impresa (Módulo 9, RF-09.1). Es una URL de <c>consultatimbre</c> en los
/// servidores de la DGII: quien recibe la RI escanea el QR y la DGII le muestra el
/// estado del e-CF (aceptado / rechazado / no encontrado).
/// <para>
/// Dos variantes según el documento (Formato e-CF + contexto DGII §K/§L):
/// </para>
/// <list type="bullet">
///   <item>e-CF normal → <c>ecf.dgii.gov.do/{ambiente}/consultatimbre</c>, 7 parámetros.</item>
///   <item>RFCE (tipo 32 &lt; DOP 250 000) → <c>fc.dgii.gov.do/{ambiente}/consultatimbrefc</c>,
///     4 parámetros (sin comprador, fecha de emisión ni fecha de firma).</item>
/// </list>
/// <para>
/// Cada valor lleva percent-encoding (<see cref="Uri.EscapeDataString(string)"/>):
/// la <c>fechafirma</c> tiene un espacio y <c>:</c>, que quedan <c>%20</c> y
/// <c>%3A</c> (RF-09.2). El <c>codigoseguridad</c> es un prefijo Base64 y se
/// preserva tal cual — es sensible a mayúsculas/minúsculas.
/// </para>
/// <para>
/// La forma exacta (formato del monto, encoding de la fecha) está construida a
/// especificación pero aún <b>sin confirmar contra la DGII real</b> — ver
/// <c>docs/verification-url.md</c>.
/// </para>
/// </summary>
public static class EcfVerificationUrl
{
    private const string EcfHost = "https://ecf.dgii.gov.do";
    private const string RfceHost = "https://fc.dgii.gov.do";

    /// <summary>
    /// La URL de verificación para <paramref name="document"/>, firmado en
    /// <paramref name="signedAt"/> con el código de seguridad
    /// <paramref name="securityCode"/> (los 6 primeros caracteres del
    /// <c>SignatureValue</c>).
    /// </summary>
    public static string For(
        EcfDocument document,
        DgiiEnvironment environment,
        string securityCode,
        DateTimeOffset signedAt)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentException.ThrowIfNullOrEmpty(securityCode);

        var header = document.Header;
        var montoTotal = document.Totals.MontoTotal.ToString("0.00", CultureInfo.InvariantCulture);

        if (document.QualifiesForRfce)
        {
            return Build($"{RfceHost}/{environment.UrlSegment}/consultatimbrefc",
            [
                ("rncemisor", header.Issuer.Rnc.Value),
                ("encf", header.Encf.Value),
                ("montototal", montoTotal),
                ("codigoseguridad", securityCode),
            ]);
        }

        return Build($"{EcfHost}/{environment.UrlSegment}/consultatimbre",
        [
            ("rncemisor", header.Issuer.Rnc.Value),
            ("rnccomprador", BuyerIdentifier(header.Buyer)),
            ("encf", header.Encf.Value),
            ("fechaemision", header.IssueDate.ToString(DominicanTimeZone.DateFormat, CultureInfo.InvariantCulture)),
            ("montototal", montoTotal),
            ("fechafirma", DominicanTimeZone.ToDateTimeString(signedAt)),
            ("codigoseguridad", securityCode),
        ]);
    }

    /// <summary>
    /// El identificador del comprador para el QR: RNC/cédula, o el identificador
    /// extranjero, o vacío (tipo 43 y otros que no llevan comprador).
    /// </summary>
    private static string BuyerIdentifier(EcfBuyer buyer)
        => buyer.Rnc?.Value ?? buyer.ForeignId ?? string.Empty;

    private static string Build(string baseUrl, ReadOnlySpan<(string Key, string Value)> parameters)
    {
        var query = new StringBuilder();

        foreach (var (key, value) in parameters)
        {
            query.Append(query.Length == 0 ? '?' : '&')
                 .Append(key)
                 .Append('=')
                 .Append(Uri.EscapeDataString(value));
        }

        return baseUrl + query;
    }
}
