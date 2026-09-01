using NovaFE.Application.Ecf.Representation;
using NovaFE.Domain.Ecf;
using NovaFE.Domain.Fiscal;
using NovaFE.Infrastructure.Ecf;
using NovaFE.Infrastructure.Ecf.Representation;

namespace NovaFE.UnitTests.Ecf;

/// <summary>
/// El lector proyecta el <c>&lt;ECF&gt;</c> que produce el serializador de Módulo 2
/// al modelo de la Representación Impresa, sin depender de la firma.
/// </summary>
public class EcfRepresentationReaderTests
{
    private static readonly EcfXmlSerializer Serializer = new();
    private static readonly EcfXmlRepresentationReader Reader = new();
    private static readonly RepresentationVerification Timbre = new("aB3xZ9", "https://ecf.dgii.gov.do/testecf/consultatimbre?x=1");

    private static RepresentationModel Read(EcfDocument document) =>
        Reader.Read(Serializer.Serialize(document, EcfTestData.SignedAt), Timbre, dgii: null);

    [Fact]
    public void Reads_the_credit_note_header_parties_and_line()
    {
        var model = Read(EcfTestData.CreditoFiscal());

        model.Document.TypeCode.ShouldBe("31");
        model.Document.TypeName.ShouldBe("Factura de Crédito Fiscal Electrónica");
        model.Document.Encf.ShouldBe("E310000000042");
        model.Document.IssueDate.ShouldBe(new DateOnly(2026, 2, 21));
        model.Document.SequenceExpiresOn.ShouldBe(new DateOnly(2027, 12, 31));
        model.Document.SignedAtText.ShouldBe("21-02-2026 10:30:05");

        model.Issuer.Name.ShouldBe("AlMax Solutions EIRL");
        model.Issuer.Rnc.ShouldNotBeNullOrEmpty();

        model.Buyer.ShouldNotBeNull();
        model.Buyer.Name.ShouldBe("Activatec SRL");
        model.Buyer.TaxId.ShouldNotBeNullOrEmpty();

        var line = model.Lines.ShouldHaveSingleItem();
        line.Number.ShouldBe(1);
        line.Name.ShouldBe("Servicio de consultoría");
        line.Kind.ShouldBe("Servicio");
        line.UnitPrice.ShouldBe(2000m);
        line.TaxLabel.ShouldBe("18%");

        model.Totals.MontoTotal.ShouldBe(2360m);
        model.Totals.TotalItbis.ShouldBe(360m);
        model.Verification.SecurityCode.ShouldBe("aB3xZ9");
    }

    [Fact]
    public void A_consumo_keeps_the_final_consumer_with_no_tax_id()
    {
        var model = Read(EcfTestData.Consumo());

        model.Document.TypeCode.ShouldBe("32");
        model.Document.SequenceExpiresOn.ShouldBeNull();
        model.Buyer.ShouldNotBeNull();
        model.Buyer.Name.ShouldBe("Consumidor Final");
        model.Buyer.TaxId.ShouldBeNull();
    }

    [Fact]
    public void Gastos_menores_has_no_buyer_block()
    {
        var model = Read(EcfTestData.GastosMenores());

        model.Buyer.ShouldBeNull();
        model.Totals.MontoExento.ShouldNotBeNull();
    }

    [Fact]
    public void Compras_carries_the_line_and_total_withholding()
    {
        var model = Read(EcfTestData.Compras());

        var line = model.Lines.ShouldHaveSingleItem();
        line.ItbisWithheld.ShouldBe(108m);
        line.IsrWithheld.ShouldBe(200m);

        model.Totals.TotalItbisWithheld.ShouldBe(108m);
        model.Totals.TotalIsrWithheld.ShouldBe(200m);
        model.Totals.AmountDue.ShouldNotBeNull();
    }

    [Fact]
    public void A_credit_note_carries_its_reference()
    {
        var model = Read(EcfTestData.NotaCredito());

        model.Reference.ShouldNotBeNull();
        model.Reference.ModifiedNcf.ShouldBe("E310000000010");
        model.Reference.ModifiedDate.ShouldNotBeNull();
        model.Reference.Reason.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void An_export_identifies_the_buyer_by_foreign_id()
    {
        var model = Read(EcfTestData.Exportaciones());

        model.Buyer.ShouldNotBeNull();
        model.Buyer.Rnc.ShouldBeNull();
        model.Buyer.ForeignId.ShouldNotBeNullOrEmpty();
        model.Buyer.TaxId.ShouldBe(model.Buyer.ForeignId);
    }

    [Fact]
    public void Reads_the_three_itbis_rate_buckets()
    {
        var model = Read(EcfTestData.CreditoFiscal(
            EcfTestData.Line(1, ItbisRate.Eighteen, unitPrice: 1000m),
            EcfTestData.Line(2, ItbisRate.Sixteen, unitPrice: 500m, name: "Seguro"),
            EcfTestData.Line(3, ItbisRate.Exempt, unitPrice: 300m, name: "Exento")));

        model.Totals.MontoGravadoI1.ShouldBe(1000m);
        model.Totals.MontoGravadoI2.ShouldBe(500m);
        model.Totals.MontoExento.ShouldBe(300m);
        model.Lines.Count.ShouldBe(3);
    }
}
