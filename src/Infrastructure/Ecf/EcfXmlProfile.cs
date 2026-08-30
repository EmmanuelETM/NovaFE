using NovaFE.Domain.Common;

namespace NovaFE.Infrastructure.Ecf;

/// <summary>Cómo se emite el bloque <c>&lt;Comprador&gt;</c> según el tipo.</summary>
internal enum CompradorShape
{
    /// <summary>RNC/IdExtranjero + razón social + contacto/correo/dirección/…</summary>
    Full,

    /// <summary>Solo identificador extranjero y razón social (tipo 47).</summary>
    Reduced,

    /// <summary>Sin bloque comprador (tipo 43).</summary>
    None,
}

/// <summary>Qué buckets tiene el <c>&lt;Totales&gt;</c> del tipo (y, por espejo, <c>&lt;OtraMoneda&gt;</c>, <c>&lt;Subtotales&gt;</c>, <c>&lt;Paginacion&gt;</c>).</summary>
internal enum TotalsShape
{
    /// <summary>Gravado I1/I2/I3 + exento + ITBIS por tasa (31/32/33/34/41/45).</summary>
    Full,

    /// <summary>Solo el bucket a tasa 0 % (tipo 46).</summary>
    ZeroRate,

    /// <summary>Solo exento (43/44/47).</summary>
    ExemptOnly,
}

/// <summary>Cómo se emite el bloque <c>&lt;Transporte&gt;</c> según el tipo.</summary>
internal enum TransportShape
{
    /// <summary>Campos básicos (conductor, placa, ruta…).</summary>
    Standard,

    /// <summary>Antepone vía/país/compañía transportista (tipo 46).</summary>
    Export,

    /// <summary>Solo <c>&lt;PaisDestino&gt;</c> (tipo 47).</summary>
    DestinationOnly,
}

/// <summary>Cómo se emite el área <c>&lt;Retencion&gt;</c> de la línea según el tipo.</summary>
internal enum RetentionShape
{
    /// <summary>ITBIS y/o ISR, condicionales a que sean &gt; 0 (tipo 41).</summary>
    Standard,

    /// <summary>Solo ISR, y el monto siempre presente aunque sea 0 (tipo 47).</summary>
    IsrOnly,
}

/// <summary>
/// Perfil de serialización XML de un tipo de e-CF: la matriz de "qué bloques y
/// campos admite su XSD". Un solo lugar que describe los diez tipos, en vez de
/// condicionales <c>doc.Type == …</c> repartidos por el serializador. Es una
/// preocupación de Infrastructure (deriva de los XSD de la DGII), no del dominio.
/// </summary>
/// <param name="MinimalIdDoc"><c>&lt;IdDoc&gt;</c> reducido: solo TipoeCF, eNCF, FechaVencimientoSecuencia, TipoPago (tipo 43).</param>
/// <param name="CreditNoteIndicator"><c>&lt;IndicadorNotaCredito&gt;</c> en vez de <c>&lt;FechaVencimientoSecuencia&gt;</c> (tipo 34).</param>
/// <param name="DeferredDeliveryIndicator">El XSD admite <c>&lt;IndicadorEnvioDiferido&gt;</c> (no en 41/47).</param>
/// <param name="TaxedIndicator">El XSD admite <c>&lt;IndicadorMontoGravado&gt;</c> (no en 44/46/47).</param>
/// <param name="IncomeType">El XSD admite <c>&lt;TipoIngresos&gt;</c> (no en 41/47).</param>
/// <param name="PaymentMethods">El XSD admite <c>&lt;TablaFormasPago&gt;</c> en el IdDoc (no en 34).</param>
/// <param name="Comprador">Forma del bloque <c>&lt;Comprador&gt;</c>.</param>
/// <param name="Totals">Buckets del <c>&lt;Totales&gt;</c>.</param>
/// <param name="ExportShipping"><c>&lt;InformacionesAdicionales&gt;</c> lleva los campos FOB/CIF/puertos (tipo 46).</param>
/// <param name="Transport">Forma del bloque <c>&lt;Transporte&gt;</c>.</param>
/// <param name="IscBreakdownAmounts">El desglose <c>&lt;ImpuestosAdicionales&gt;</c> lleva montos de ISC específico/ad valorem (no en 44).</param>
/// <param name="Mining">El <c>&lt;Item&gt;</c> admite el bloque <c>&lt;Mineria&gt;</c> (32/33/34/46).</param>
/// <param name="Retention">Forma del área <c>&lt;Retencion&gt;</c> de la línea.</param>
internal sealed record EcfXmlProfile(
    bool MinimalIdDoc = false,
    bool CreditNoteIndicator = false,
    bool DeferredDeliveryIndicator = true,
    bool TaxedIndicator = true,
    bool IncomeType = true,
    bool PaymentMethods = true,
    CompradorShape Comprador = CompradorShape.Full,
    TotalsShape Totals = TotalsShape.Full,
    bool ExportShipping = false,
    TransportShape Transport = TransportShape.Standard,
    bool IscBreakdownAmounts = true,
    bool Mining = false,
    RetentionShape Retention = RetentionShape.Standard);

/// <summary>Perfil XML por tipo de e-CF. Los defaults del record describen el tipo 31/45.</summary>
internal static class EcfXmlProfiles
{
    private static readonly EcfXmlProfile Standard = new();

    public static EcfXmlProfile For(EcfType type) => type.Id switch
    {
        31 => Standard,                                                     // Crédito Fiscal
        32 => Standard with { Mining = true },                              // Consumo
        33 => Standard with { Mining = true },                              // Nota de Débito
        34 => Standard with { CreditNoteIndicator = true, PaymentMethods = false, Mining = true },
        41 => Standard with { DeferredDeliveryIndicator = false, IncomeType = false },   // Compras
        43 => Standard with                                                 // Gastos Menores
        {
            MinimalIdDoc = true,
            Comprador = CompradorShape.None,
            Totals = TotalsShape.ExemptOnly,
        },
        44 => Standard with                                                 // Regímenes Especiales
        {
            TaxedIndicator = false,
            Totals = TotalsShape.ExemptOnly,
            IscBreakdownAmounts = false,
        },
        45 => Standard,                                                     // Gubernamental
        46 => Standard with                                                 // Exportaciones
        {
            TaxedIndicator = false,
            Totals = TotalsShape.ZeroRate,
            ExportShipping = true,
            Transport = TransportShape.Export,
            Mining = true,
        },
        47 => Standard with                                                 // Pagos al Exterior
        {
            DeferredDeliveryIndicator = false,
            TaxedIndicator = false,
            IncomeType = false,
            Comprador = CompradorShape.Reduced,
            Totals = TotalsShape.ExemptOnly,
            Transport = TransportShape.DestinationOnly,
            Retention = RetentionShape.IsrOnly,
        },
        _ => throw new NotSupportedException($"No hay perfil de serialización XML para el tipo de e-CF {type.Id}."),
    };
}
