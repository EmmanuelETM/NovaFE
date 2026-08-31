using NovaFE.Application.Ecf.Contracts;
using NovaFE.Application.Ecf.IssueEcf;
using NovaFE.Domain.Common;
using NovaFE.Domain.Ecf;
using NovaFE.Domain.Sequences;

namespace NovaFE.UnitTests.Ecf;

public class EcfDocumentMapperTests
{
    private static readonly EcfIssuer Issuer = EcfTestData.Issuer();
    private static readonly DateOnly IssueDate = EcfTestData.IssueDate;

    private static EcfDocumentMapperResult Map(int typeCode, IssueEcfCommand command)
    {
        var type = EcfType.FromValue(typeCode);
        var encf = Encf.Build('E', typeCode, 1);
        var expiry = type.HasSequenceExpiry ? new DateOnly(IssueDate.Year + 1, 12, 31) : (DateOnly?)null;
        var result = EcfDocumentMapper.ToDocument(command, type, encf, expiry, Issuer, IssueDate);
        return new EcfDocumentMapperResult(result.IsError, result.IsError ? null : result.Value,
            result.IsError ? result.FirstError.Code : null);
    }

    private sealed record EcfDocumentMapperResult(bool IsError, EcfDocument? Document, string? ErrorCode);

    private static IssueEcfCommand CreditoFiscal() => new()
    {
        Type = 31,
        IncomeType = "01",
        Buyer = new EcfBuyerPayload(Name: "Cliente SRL", Rnc: "131880681"),
        Payment = new EcfPaymentPayload(Condition: "credit", DueDate: new DateOnly(2026, 3, 15),
            Methods: [new EcfPaymentMethodPayload("check_transfer", 2360m)]),
        Lines = [new EcfLinePayload("Servicio", Kind: "service", Quantity: 1, UnitPrice: 2000m, ItbisRate: 1, UnitOfMeasure: "43")],
    };

    [Fact]
    public void Maps_a_credit_fiscal_and_it_balances()
    {
        var result = Map(31, CreditoFiscal());

        result.IsError.ShouldBeFalse();
        var doc = result.Document!;
        doc.Type.ShouldBe(EcfType.CreditoFiscal);
        doc.Header.Issuer.Rnc.ShouldBe(Issuer.Rnc);
        doc.Header.Buyer.Rnc!.Value.Value.ShouldBe("131880681");
        doc.Header.Payment.Condition.ShouldBe(PaymentCondition.Credit);
        doc.Header.Payment.Methods[0].Method.ShouldBe(PaymentMethodType.CheckTransfer);
        doc.Totals.MontoTotal.ShouldBe(2360m);
        doc.Header.SequenceExpiresOn.ShouldNotBeNull();
    }

    [Fact]
    public void Enum_fields_accept_names_or_dgii_codes()
    {
        var byName = Map(31, CreditoFiscal() with
        {
            Payment = new EcfPaymentPayload("Credit", new DateOnly(2026, 3, 15),
                [new EcfPaymentMethodPayload("CheckTransfer", 2360m)]),
        });
        var byCode = Map(31, CreditoFiscal() with
        {
            Payment = new EcfPaymentPayload("2", new DateOnly(2026, 3, 15),
                [new EcfPaymentMethodPayload("2", 2360m)]),
        });

        byName.IsError.ShouldBeFalse();
        byCode.IsError.ShouldBeFalse();
        byName.Document!.Header.Payment.Methods[0].Method.ShouldBe(PaymentMethodType.CheckTransfer);
        byCode.Document!.Header.Payment.Condition.ShouldBe(PaymentCondition.Credit);
    }

    [Fact]
    public void An_unknown_enum_value_is_a_validation_error()
    {
        var result = Map(31, CreditoFiscal() with
        {
            Payment = new EcfPaymentPayload("layaway", null, null),
        });

        result.IsError.ShouldBeTrue();
        result.ErrorCode.ShouldBe("Ecf.InvalidPayload");
    }

    [Fact]
    public void Maps_a_low_amount_consumo_that_qualifies_for_rfce()
    {
        var result = Map(32, new IssueEcfCommand
        {
            Type = 32,
            IncomeType = "01",
            Payment = new EcfPaymentPayload("cash", Methods: [new EcfPaymentMethodPayload("cash", 1180m)]),
            Lines = [new EcfLinePayload("Almuerzo", Kind: "good", Quantity: 1, UnitPrice: 1000m, ItbisRate: 1, UnitOfMeasure: "43")],
        });

        result.IsError.ShouldBeFalse();
        result.Document!.QualifiesForRfce.ShouldBeTrue();
        result.Document.Header.SequenceExpiresOn.ShouldBeNull();
    }

    [Fact]
    public void Maps_a_compras_with_line_retention()
    {
        var result = Map(41, new IssueEcfCommand
        {
            Type = 41,
            Buyer = new EcfBuyerPayload(Name: "Proveedor informal", Rnc: "131880681"),
            Payment = new EcfPaymentPayload("cash", Methods: [new EcfPaymentMethodPayload("cash", 2000m)]),
            Lines =
            [
                new EcfLinePayload("Compra", Kind: "good", Quantity: 1, UnitPrice: 2000m, ItbisRate: 4, UnitOfMeasure: "43",
                    Retention: new EcfLineRetentionPayload("withholding", ItbisWithheld: 0m, IsrWithheld: 200m)),
            ],
        });

        result.IsError.ShouldBeFalse();
        result.Document!.Lines[0].Retention!.IsrWithheld.ShouldBe(200m);
        result.Document.Totals.TotalIsrWithheld.ShouldBe(200m);
    }

    [Fact]
    public void Passes_through_shipping_and_transport_for_an_export()
    {
        var result = Map(46, new IssueEcfCommand
        {
            Type = 46,
            IncomeType = "01",
            Buyer = new EcfBuyerPayload(Name: "Global Imports LLC", ForeignId: "US-4471203"),
            Payment = new EcfPaymentPayload("cash", Methods: [new EcfPaymentMethodPayload("cash", 15000m)]),
            Lines = [new EcfLinePayload("Cacao", Kind: "good", Quantity: 1, UnitPrice: 15000m, ItbisRate: 3, UnitOfMeasure: "43")],
            Shipping = new EcfShippingPayload(ReferenceNumber: "7788",
                Export: new EcfExportPayload(LoadingPortName: "Puerto Haina", DeliveryTerms: "FOB",
                    TotalFob: 15000m, Insurance: 300m, Freight: 1200m, TotalCif: 16500m)),
            Transport = new EcfTransportPayload(Via: "sea", DestinationCountry: "Estados Unidos", CarrierName: "Maersk"),
        });

        result.IsError.ShouldBeFalse();
        result.Document!.Header.Shipping!.Export!.TotalCif.ShouldBe(16500m);
        result.Document.Header.Transport!.Via.ShouldBe(TransportVia.Sea);
    }

    [Fact]
    public void Maps_line_level_foreign_currency_and_details()
    {
        var result = Map(31, CreditoFiscal() with
        {
            ForeignCurrency = new EcfForeignCurrencyPayload("USD", 58.50m,
                new EcfForeignCurrencyTotalsPayload(MontoGravadoTotal: 34.19m, MontoTotal: 40.34m)),
            Lines =
            [
                new EcfLinePayload("Ron añejo", Kind: "good", Quantity: 1, UnitPrice: 2000m, ItbisRate: 1, UnitOfMeasure: "43",
                    ForeignCurrency: new EcfLineForeignCurrencyPayload(UnitPrice: 34.19m, LineAmount: 34.19m),
                    Details: new EcfLineDetailsPayload(
                        AlcoholDegrees: 40m, ReferenceQuantity: 0.75m, ReferenceUnit: "43",
                        Subquantities: [new EcfSubquantityPayload(3m, "43")])),
            ],
        });

        result.IsError.ShouldBeFalse();
        var line = result.Document!.Lines[0];
        line.ForeignCurrency!.UnitPrice.ShouldBe(34.19m);
        line.Details!.AlcoholDegrees.ShouldBe(40m);
        line.Details.Subquantities!.ShouldHaveSingleItem();
        line.Details.Subquantities[0].Quantity.ShouldBe(3m);
    }

    [Fact]
    public void Threads_declared_amount_into_the_tolerance_report()
    {
        var result = Map(31, CreditoFiscal() with
        {
            Lines = [new EcfLinePayload("Servicio", Kind: "service", Quantity: 1, UnitPrice: 2000m,
                ItbisRate: 1, UnitOfMeasure: "43", DeclaredAmount: 2010m)],
        });

        result.IsError.ShouldBeFalse();
        var tolerance = result.Document!.Calculation.Tolerance;
        tolerance.LineDiffs.ShouldHaveSingleItem();
        tolerance.LineDiffs[0].Supplied.ShouldBe(2010m);
        tolerance.LineDiffs[0].Calculated.ShouldBe(2000m);
    }
}
