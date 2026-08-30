using NovaFE.Domain.Common;
using NovaFE.Domain.Ecf;
using NovaFE.Domain.Fiscal;
using NovaFE.Domain.Sequences;

namespace NovaFE.UnitTests.Ecf;

/// <summary>Constructores para armar documentos e-CF válidos en las pruebas.</summary>
internal static class EcfTestData
{
    public static readonly DateOnly IssueDate = new(2026, 2, 21);
    public static readonly DateTimeOffset SignedAt = new(2026, 2, 21, 14, 30, 5, TimeSpan.Zero);

    public static EcfIssuer Issuer() => new(
        Rnc: Rnc.FromStorage("132786262"),
        Name: "AlMax Solutions EIRL",
        Address: "Carretera Mella 1099, Santo Domingo",
        Email: "facturacion@almax.do");

    public static EcfBuyer Buyer() => new(
        Name: "Activatec SRL",
        Rnc: Rnc.FromStorage("132056892"),
        Email: "info@activatec.do");

    public static EcfPayment Payment() => new(
        Condition: PaymentCondition.Credit,
        DueDate: new DateOnly(2026, 3, 15),
        Methods: [new EcfPaymentMethod(PaymentMethodType.CheckTransfer, 2360.00m)]);

    public static EcfHeader Header(
        int typeCode = 31,
        DateOnly? sequenceExpiresOn = null,
        bool pricesIncludeTax = false,
        decimal nonInvoiceableAmount = 0m)
        => new(
            Encf: Encf.Build('E', typeCode, 42),
            SequenceExpiresOn: sequenceExpiresOn ?? new DateOnly(2027, 12, 31),
            IssueDate: IssueDate,
            IncomeType: "01",
            PricesIncludeTax: pricesIncludeTax,
            Issuer: Issuer(),
            Buyer: Buyer(),
            Payment: Payment(),
            NonInvoiceableAmount: nonInvoiceableAmount);

    public static EcfLine Line(
        int number = 1,
        ItbisRate? rate = null,
        decimal quantity = 1m,
        decimal unitPrice = 2000.00m,
        string name = "Servicio de consultoría",
        ItemKind? kind = null,
        EcfLineRetention? retention = null)
        => new(
            Number: number,
            Rate: rate ?? ItbisRate.Eighteen,
            Name: name,
            Kind: kind ?? ItemKind.Service,
            Quantity: quantity,
            UnitPrice: unitPrice,
            UnitOfMeasure: "43",
            Retention: retention);

    /// <summary>
    /// Comprobante de Gastos Menores (tipo 43): caja chica. El más reducido —
    /// sin bloque comprador, sin formas de pago, líneas exentas y sin ajustes.
    /// </summary>
    public static EcfDocument GastosMenores(params EcfLine[] lines)
        => EcfDocument.Create(
            EcfType.GastosMenores,
            Header(43),
            lines.Length == 0 ? [Line(rate: ItbisRate.Exempt, unitPrice: 350m, name: "Café y agua para reunión")] : lines).Value;

    /// <summary>
    /// Comprobante de Regímenes Especiales (tipo 44): zona franca / regímenes de
    /// incentivo. Todo exento — su XSD no tiene campos gravados en <c>&lt;Totales&gt;</c>.
    /// </summary>
    public static EcfDocument RegimenesEspeciales(params EcfLine[] lines)
        => EcfDocument.Create(
            EcfType.RegimenesEspeciales,
            Header(44),
            lines.Length == 0 ? [Line(rate: ItbisRate.Exempt)] : lines).Value;

    /// <summary>
    /// Comprobante Gubernamental (tipo 45): venta a una entidad del Estado. El XSD
    /// es igual al del 31 (IdDoc, Totales, Comprador con RNC obligatorio).
    /// </summary>
    public static EcfDocument Gubernamental(params EcfLine[] lines)
        => EcfDocument.Create(
            EcfType.Gubernamental,
            Header(45),
            lines.Length == 0 ? [Line()] : lines).Value;

    /// <summary>Retención típica de un servicio: 30 % del ITBIS y 10 % de honorarios de ISR.</summary>
    public static EcfLineRetention Retention(decimal itbisWithheld = 108.00m, decimal isrWithheld = 200.00m)
        => new(RetentionAgent.Withholding, itbisWithheld, isrWithheld);

    /// <summary>
    /// Comprobante de Compras (tipo 41): el emisor registra una compra a un
    /// proveedor informal y actúa como agente de retención. Cada línea lleva
    /// <c>&lt;Retencion&gt;</c>; el IdDoc no lleva <c>&lt;TipoIngresos&gt;</c>.
    /// </summary>
    public static EcfDocument Compras(params EcfLine[] lines)
        => EcfDocument.Create(
            EcfType.Compras,
            Header(41),
            lines.Length == 0 ? [Line(retention: Retention())] : lines).Value;

    public static EcfDocument CreditoFiscal(params EcfLine[] lines)
        => EcfDocument.Create(
            EcfType.CreditoFiscal,
            Header(31),
            lines.Length == 0 ? [Line()] : lines).Value;

    /// <summary>
    /// Factura de Consumo (tipo 32). Sin vencimiento de secuencia; el comprador
    /// solo se identifica si el monto llega a DOP 250 000 (aquí no).
    /// </summary>
    public static EcfDocument Consumo(params EcfLine[] lines)
        => EcfDocument.Create(
            EcfType.Consumo,
            Header(32) with { SequenceExpiresOn = null, Buyer = new EcfBuyer("Consumidor Final") },
            lines.Length == 0 ? [Line()] : lines).Value;

    /// <summary>
    /// Nota de Débito (tipo 33) que modifica un e-CF de crédito fiscal. Mantiene
    /// el vencimiento de secuencia y las formas de pago (igual que el 31); lo
    /// propio es la <c>InformacionReferencia</c> obligatoria.
    /// </summary>
    public static EcfDocument NotaDebito(
        EcfReference? reference = null,
        params EcfLine[] lines)
        => EcfDocument.Create(
            EcfType.NotaDebito,
            Header(33),
            lines.Length == 0 ? [Line()] : lines,
            reference ?? new EcfReference(
                "E310000000010",
                IssueDate.AddDays(-10),
                ModificationCode.CorrectsAmounts)).Value;

    /// <summary>
    /// Nota de Crédito (tipo 34) que modifica un e-CF de crédito fiscal 20 días
    /// antes (indicador dentro de 30 días → 0). Sin vencimiento de secuencia.
    /// </summary>
    public static EcfDocument NotaCredito(
        EcfReference? reference = null,
        params EcfLine[] lines)
        => EcfDocument.Create(
            EcfType.NotaCredito,
            Header(34) with { SequenceExpiresOn = null },
            lines.Length == 0 ? [Line()] : lines,
            reference ?? new EcfReference(
                "E310000000010",
                IssueDate.AddDays(-20),
                ModificationCode.CorrectsAmounts)).Value;
}
