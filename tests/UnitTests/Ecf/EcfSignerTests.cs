using ErrorOr;
using NSubstitute;
using NovaFE.Application.Ecf;
using NovaFE.Application.Signing.Contracts;
using NovaFE.Application.Signing.Interfaces;
using NovaFE.Domain.Common;
using NovaFE.Domain.Ecf;
using NovaFE.Infrastructure.Ecf;
using NovaFE.UnitTests.Common;

namespace NovaFE.UnitTests.Ecf;

/// <summary>
/// <see cref="EcfSigner"/> con serializadores y validador XSD <b>reales</b> — solo
/// la firma criptográfica (<see cref="ICertificateSigner"/>) está sustituida. La
/// firma de mentira agrega un <c>&lt;Signature&gt;</c> de relleno para que el XML
/// firmado valide contra el XSD (que exige el slot de firma).
/// </summary>
public class EcfSignerTests : UseCaseTestBase
{
    private const string StubSignatureValue = "AbCdEfGhIjKlMnOp";
    private const string StubSecurityCode = "AbCdEf";

    private readonly ICertificateSigner _certificateSigner = Substitute.For<ICertificateSigner>();

    public EcfSignerTests()
    {
        _certificateSigner
            .SignAsync(Arg.Any<string>(), Arg.Any<DgiiEnvironment>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult<ErrorOr<SignedXmlResult>>(new SignedXmlResult(
                Xml: WithStubSignature(call.Arg<string>()),
                SignatureValue: StubSignatureValue,
                SecurityCode: StubSecurityCode)));
    }

    private EcfSigner Sut() => new(
        new EcfXmlSerializer(),
        new RfceSerializer(),
        new EcfXsdValidator(),
        _certificateSigner,
        Clock);

    [Fact]
    public async Task Serializes_signs_validates_and_hashes_the_document()
    {
        var result = await Sut().SignAsync(EcfTestData.CreditoFiscal(), DgiiEnvironment.Test);

        result.IsError.ShouldBeFalse();
        var signed = result.Value;
        signed.EcfXml.ShouldContain("<Signature");
        signed.EcfXml.ShouldContain("<TipoeCF>31</TipoeCF>");
        signed.SignatureValue.ShouldBe(StubSignatureValue);
        signed.SecurityCode.ShouldBe(StubSecurityCode);
        signed.DocumentHash.Length.ShouldBe(64); // SHA-256 en hex
        signed.RfceXml.ShouldBeNull();
        signed.SubmitsRfce.ShouldBeFalse();
        signed.QrUrl.ShouldStartWith("https://ecf.dgii.gov.do/testecf/consultatimbre?");
        signed.QrUrl.ShouldContain($"codigoseguridad={StubSecurityCode}");
    }

    [Fact]
    public async Task Stamps_the_signing_instant_from_the_clock()
    {
        var result = await Sut().SignAsync(EcfTestData.CreditoFiscal(), DgiiEnvironment.Test);

        result.Value.SignedAt.ShouldBe(Clock.GetUtcNow());
        // dd-MM-yyyy HH:mm:ss en hora dominicana (UTC-4): 2026-01-15 10:30 UTC → 06:30.
        result.Value.EcfXml.ShouldContain("<FechaHoraFirma>15-01-2026 06:30:00</FechaHoraFirma>");
    }

    [Fact]
    public async Task A_low_amount_consumo_also_produces_the_signed_rfce()
    {
        var document = EcfTestData.Consumo();
        document.QualifiesForRfce.ShouldBeTrue();

        var result = await Sut().SignAsync(document, DgiiEnvironment.Test);

        result.IsError.ShouldBeFalse();
        result.Value.SubmitsRfce.ShouldBeTrue();
        result.Value.RfceXml.ShouldNotBeNull();
        result.Value.RfceXml.ShouldContain("<RFCE>");
        result.Value.RfceXml.ShouldContain("<Signature");   // el RFCE también se firma
        // El resumen queda atado al e-CF por el código de seguridad de la firma.
        result.Value.RfceXml.ShouldContain($"<CodigoSeguridadeCF>{StubSecurityCode}</CodigoSeguridadeCF>");
        result.Value.RfceXml.ShouldNotContain("<DetallesItems>");
        result.Value.QrUrl.ShouldStartWith("https://fc.dgii.gov.do/testecf/consultatimbrefc?");
        // El <ECF> completo igual se firma y se guarda.
        result.Value.EcfXml.ShouldContain("<TipoeCF>32</TipoeCF>");
        // Dos firmas: una para el e-CF, otra para el RFCE.
        await _certificateSigner.Received(2).SignAsync(
            Arg.Any<string>(), DgiiEnvironment.Test, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_consumo_at_or_above_the_threshold_is_a_full_ecf_not_an_rfce()
    {
        var document = EcfDocument.Create(
            EcfType.Consumo,
            EcfTestData.Header(32) with
            {
                SequenceExpiresOn = null,
                Buyer = EcfTestData.Buyer(),
            },
            [EcfTestData.Line(unitPrice: 300_000m, name: "Equipo industrial")]).Value;
        document.QualifiesForRfce.ShouldBeFalse();

        var result = await Sut().SignAsync(document, DgiiEnvironment.Test);

        result.IsError.ShouldBeFalse();
        result.Value.SubmitsRfce.ShouldBeFalse();
        result.Value.RfceXml.ShouldBeNull();
    }

    [Fact]
    public async Task Propagates_the_certificate_signer_error()
    {
        _certificateSigner
            .SignAsync(Arg.Any<string>(), Arg.Any<DgiiEnvironment>(), Arg.Any<CancellationToken>())
            .Returns(Error.Failure("Certificate.NoActiveCertificate", "no hay certificado activo"));

        var result = await Sut().SignAsync(EcfTestData.CreditoFiscal(), DgiiEnvironment.Production);

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("Certificate.NoActiveCertificate");
    }

    [Fact]
    public async Task Fails_when_the_signed_xml_does_not_validate_against_the_xsd()
    {
        _certificateSigner
            .SignAsync(Arg.Any<string>(), Arg.Any<DgiiEnvironment>(), Arg.Any<CancellationToken>())
            .Returns(new SignedXmlResult("<ECF><Encabezado/></ECF>", StubSignatureValue, StubSecurityCode));

        var result = await Sut().SignAsync(EcfTestData.CreditoFiscal(), DgiiEnvironment.Test);

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("Ecf.SignedDocumentFailedXsd");
        result.FirstError.Type.ShouldBe(ErrorType.Unexpected);
    }

    private static string WithStubSignature(string xml)
    {
        const string signature = "<Signature xmlns=\"http://www.w3.org/2000/09/xmldsig#\"/>";
        return xml
            .Replace("</ECF>", signature + "</ECF>", StringComparison.Ordinal)
            .Replace("</RFCE>", signature + "</RFCE>", StringComparison.Ordinal);
    }
}
