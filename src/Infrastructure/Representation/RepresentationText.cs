using System.Globalization;
using NovaFE.Application.Ecf.Representation;

namespace NovaFE.Infrastructure.Representation;

/// <summary>
/// Formato compartido por los dos layouts de la Representación Impresa (Carta y
/// POS): montos en pesos, cantidades, fechas, el sello de estado DGII y el
/// dominio de verificación del timbre. Texto en español — es lo que la RI emite.
/// </summary>
internal static class RepresentationText
{
    /// <summary>Proveedor de la solución de facturación electrónica.</summary>
    public const string Vendor = "Nemus Systems";

    /// <summary>Atribución discreta para el pie de la RI ("powered by"). Nemus es
    /// el proveedor tecnológico, no el emisor del comprobante.</summary>
    public const string PoweredBy = "Con tecnología de " + Vendor;

    private static readonly NumberFormatInfo Pesos = new()
    {
        NumberGroupSeparator = ",",
        NumberDecimalSeparator = ".",
        NumberDecimalDigits = 2,
    };

    /// <summary>Monto con el prefijo <c>RD$</c> y dos decimales (<c>RD$ 1,180.00</c>).</summary>
    public static string Money(decimal value) => "RD$ " + value.ToString("N2", Pesos);

    /// <summary>El monto sin prefijo, para columnas apretadas (<c>1,180.00</c>).</summary>
    public static string Amount(decimal value) => value.ToString("N2", Pesos);

    /// <summary>Tipo de cambio y otros ratios: hasta 4 decimales, sin ceros de más.</summary>
    public static string Rate(decimal value) => value.ToString("0.####", Pesos);

    /// <summary>Cantidad: hasta 3 decimales, sin ceros de más (<c>1</c>, <c>2.5</c>).</summary>
    public static string Qty(decimal value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>Fecha de calendario en el formato de la DGII (<c>dd-MM-yyyy</c>).</summary>
    public static string Date(DateOnly date) => date.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture);

    /// <summary>Host + última ruta de la URL del timbre (<c>ecf.dgii.gov.do/consultatimbre</c>).</summary>
    public static string VerificationEndpoint(string qrUrl)
    {
        if (!Uri.TryCreate(qrUrl, UriKind.Absolute, out var uri))
            return "dgii.gov.do";

        var page = uri.Segments.LastOrDefault()?.Trim('/');
        return string.IsNullOrEmpty(page) ? uri.Host : $"{uri.Host}/{page}";
    }

    /// <summary>Etiqueta del sello de estado DGII.</summary>
    public static string StatusLabel(RepresentationDgiiStatus dgii) => dgii.Status switch
    {
        "accepted" => "ACEPTADO POR LA DGII",
        "accepted_conditional" => "ACEPTADO CONDICIONAL",
        "rejected" => "RECHAZADO POR LA DGII",
        "review" => "EN REVISIÓN",
        "submitted" => "EN PROCESO EN LA DGII",
        "failed" => "ENVÍO PENDIENTE",
        _ => "PENDIENTE DE ENVÍO",
    };

    /// <summary>Colores (tinta, fondo) del sello de estado DGII.</summary>
    public static (string Ink, string Bg) StatusColors(RepresentationDgiiStatus dgii) => dgii.Status switch
    {
        "accepted" or "accepted_conditional" => (RepresentationTheme.OkInk, RepresentationTheme.OkBg),
        "rejected" => (RepresentationTheme.BadInk, RepresentationTheme.BadBg),
        _ => (RepresentationTheme.WaitInk, RepresentationTheme.WaitBg),
    };
}
