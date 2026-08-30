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
        ItemKind? kind = null)
        => new(
            Number: number,
            Rate: rate ?? ItbisRate.Eighteen,
            Name: name,
            Kind: kind ?? ItemKind.Service,
            Quantity: quantity,
            UnitPrice: unitPrice,
            UnitOfMeasure: "43");

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
