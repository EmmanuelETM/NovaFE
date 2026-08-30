using NovaFE.Domain.Common;
using NovaFE.Domain.Ecf;
using NovaFE.Domain.Fiscal;
using NovaFE.Domain.Sequences;

namespace NovaFE.Service.DevTools;

/// <summary>Un e-CF de ejemplo — solo para el endpoint de preview en Development.</summary>
public sealed record EcfSample(string Slug, string Title, EcfDocument Document);

/// <summary>
/// Catálogo de e-CF de ejemplo, uno por tipo. Alimenta
/// <c>GET /api/v1/dev/ecf-preview/samples</c>. Para combinaciones más ricas
/// (OtraMoneda, Sección D, desglose ISC…) está la galería del proyecto de pruebas
/// (<c>EcfXmlGallery</c>) o el <c>POST</c> del mismo endpoint.
/// </summary>
public static class EcfSampleCatalog
{
    /// <summary>Fecha/hora de firma fija para que los ejemplos sean reproducibles.</summary>
    public static readonly DateTimeOffset SignedAt = new(2026, 2, 21, 14, 30, 5, TimeSpan.Zero);

    private static readonly DateOnly IssueDate = new(2026, 2, 21);
    private static readonly DateOnly SequenceExpiry = new(2027, 12, 31);

    private static readonly EcfBuyer ConsumidorFinal = new("Consumidor Final");

    private static readonly EcfReference SampleReference =
        new("E310000000010", new DateOnly(2026, 2, 11), ModificationCode.CorrectsAmounts);

    public static IReadOnlyList<EcfSample> All { get; } =
    [
        new("credito-fiscal", "Crédito fiscal (31)",
            EcfDocument.Create(EcfType.CreditoFiscal, Header(31), [Line()]).Value),

        new("consumo", "Factura de consumo (32), bajo monto",
            EcfDocument.Create(EcfType.Consumo,
                Header(32) with { SequenceExpiresOn = null, Buyer = ConsumidorFinal },
                [Line()]).Value),

        new("nota-debito", "Nota de débito (33)",
            EcfDocument.Create(EcfType.NotaDebito, Header(33), [Line()], SampleReference).Value),

        new("nota-credito", "Nota de crédito (34), dentro de 30 días",
            EcfDocument.Create(EcfType.NotaCredito,
                Header(34) with { SequenceExpiresOn = null }, [Line()], SampleReference).Value),

        new("compras", "Compras (41) a informal, con retención por línea",
            EcfDocument.Create(EcfType.Compras, Header(41),
                [Line() with { Retention = new EcfLineRetention(RetentionAgent.Withholding, 108m, 200m) }]).Value),

        new("gastos-menores", "Gastos menores (43), sin comprador",
            EcfDocument.Create(EcfType.GastosMenores, Header(43),
                [Line(rate: ItbisRate.Exempt, unitPrice: 350m, name: "Café para reunión")]).Value),

        new("regimenes-especiales", "Regímenes especiales (44), todo exento",
            EcfDocument.Create(EcfType.RegimenesEspeciales, Header(44),
                [Line(rate: ItbisRate.Exempt)]).Value),

        new("gubernamental", "Gubernamental (45)",
            EcfDocument.Create(EcfType.Gubernamental, Header(45), [Line()]).Value),

        new("exportacion", "Exportaciones (46), ITBIS a tasa 0 %",
            EcfDocument.Create(EcfType.Exportaciones,
                Header(46) with { Buyer = new EcfBuyer("Global Imports LLC", ForeignId: "US-4471203") },
                [Line(rate: ItbisRate.Zero, unitPrice: 15000m, name: "Cacao orgánico", kind: ItemKind.Good)]).Value),

        new("pagos-al-exterior", "Pagos al exterior (47), retención de solo ISR",
            EcfDocument.Create(EcfType.PagosExterior,
                Header(47) with { Buyer = new EcfBuyer("Consultancy Group Ltd.", ForeignId: "GB-882910") },
                [Line(rate: ItbisRate.Exempt, unitPrice: 50000m, name: "Consultoría internacional",
                    retention: new EcfLineRetention(RetentionAgent.Withholding, IsrWithheld: 13500m))]).Value),
    ];

    public static EcfSample? Find(string slug) =>
        All.FirstOrDefault(sample => string.Equals(sample.Slug, slug, StringComparison.OrdinalIgnoreCase));

    private static EcfHeader Header(int typeCode) => new(
        Encf: Encf.Build('E', typeCode, 42),
        SequenceExpiresOn: SequenceExpiry,
        IssueDate: IssueDate,
        IncomeType: "01",
        PricesIncludeTax: false,
        Issuer: new EcfIssuer(
            Rnc: Rnc.FromStorage("132786262"),
            Name: "AlMax Solutions EIRL",
            Address: "Carretera Mella 1099, Santo Domingo",
            Email: "facturacion@almax.do"),
        Buyer: new EcfBuyer("Activatec SRL", Rnc: Rnc.FromStorage("132056892"), Email: "info@activatec.do"),
        Payment: new EcfPayment(
            PaymentCondition.Credit,
            new DateOnly(2026, 3, 15),
            [new EcfPaymentMethod(PaymentMethodType.CheckTransfer, 2360.00m)]));

    private static EcfLine Line(
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
}
