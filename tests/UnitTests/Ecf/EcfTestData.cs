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
}
