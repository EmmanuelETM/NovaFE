using NovaFE.Domain.Common;
using NovaFE.Domain.Ecf;

namespace NovaFE.UnitTests.Ecf;

public class IssuedEcfTests
{
    private static SignedEcf Signed(string ecfXml = "<ECF/>", string? rfceXml = null) => new(
        SignedAt: EcfTestData.SignedAt,
        EcfXml: ecfXml,
        RfceXml: rfceXml,
        SignatureValue: "aB3xZ9KkLlMm",
        SecurityCode: "aB3xZ9",
        DocumentHash: new string('a', 64),
        QrUrl: "https://ecf.dgii.gov.do/testecf/consultatimbre?x=1");

    [Fact]
    public void FromSigned_snapshots_the_document_and_the_signature()
    {
        var document = EcfTestData.CreditoFiscal();

        var ecf = IssuedEcf.FromSigned(document, Signed(), DgiiEnvironment.Test);

        ecf.Id.ShouldNotBe(Guid.Empty);
        ecf.Type.ShouldBe(document.Type);
        ecf.Environment.ShouldBe(DgiiEnvironment.Test);
        ecf.Encf.ShouldBe(document.Header.Encf);
        ecf.IssueDate.ShouldBe(document.Header.IssueDate);
        ecf.SequenceExpiresOn.ShouldBe(document.Header.SequenceExpiresOn);
        ecf.Status.ShouldBe(EcfStatus.Signed);
        ecf.MontoTotal.ShouldBe(document.Totals.MontoTotal);
        ecf.Totals.MontoTotal.ShouldBe(document.Totals.MontoTotal);
        ecf.Totals.TotalItbis1.ShouldBe(document.Totals.Itbis1);
        ecf.SecurityCode.ShouldBe("aB3xZ9");
        ecf.DocumentHash.Length.ShouldBe(64);
        ecf.QrUrl.ShouldContain("consultatimbre");
        ecf.SubmitsRfce.ShouldBeFalse();
        ecf.RfceXml.ShouldBeNull();
        ecf.BuyerRnc.ShouldBe(document.Header.Buyer.Rnc?.Value);
        ecf.BuyerName.ShouldBe(document.Header.Buyer.Name);
    }

    [Fact]
    public void FromSigned_keeps_the_signed_rfce_for_a_low_amount_consumo()
    {
        var document = EcfTestData.Consumo();
        document.QualifiesForRfce.ShouldBeTrue();

        var ecf = IssuedEcf.FromSigned(document, Signed(rfceXml: "<RFCE/>"), DgiiEnvironment.Test);

        ecf.SubmitsRfce.ShouldBeTrue();
        ecf.RfceXml.ShouldBe("<RFCE/>");
        ecf.SequenceExpiresOn.ShouldBeNull();   // tipo 32 no lleva vencimiento
    }

    [Fact]
    public void FromSigned_carries_the_internal_invoice_number_for_dedup()
    {
        var document = EcfDocument.Create(
            EcfType.CreditoFiscal,
            EcfTestData.Header() with { Issuer = EcfTestData.Issuer() with { InternalInvoiceNumber = "FAC-2026-42" } },
            [EcfTestData.Line()]).Value;

        var ecf = IssuedEcf.FromSigned(document, Signed(), DgiiEnvironment.Production);

        ecf.InternalInvoiceNumber.ShouldBe("FAC-2026-42");
    }
}
