using System.Security.Cryptography.X509Certificates;
using NSubstitute;
using NovaFE.Application.Certificates.Interfaces;
using NovaFE.Application.Common.Interfaces;
using NovaFE.Application.Signing;
using NovaFE.Application.Signing.Contracts;
using NovaFE.Application.Signing.Interfaces;
using NovaFE.Domain.Certificates;
using NovaFE.Domain.Common;
using NovaFE.UnitTests.Certificates;
using NovaFE.UnitTests.Common;

namespace NovaFE.UnitTests.Signing;

public class CertificateSignerTests : UseCaseTestBase
{
    private const string Xml = "<ECF xmlns=\"x\"><A>1</A></ECF>";

    private readonly ICurrentTenant _tenant = Substitute.For<ICurrentTenant>();
    private readonly ICertificateRepository _certificates = Substitute.For<ICertificateRepository>();
    private readonly ICertificateVault _vault = Substitute.For<ICertificateVault>();
    private readonly IXmlSigner _xmlSigner = Substitute.For<IXmlSigner>();

    public CertificateSignerTests()
    {
        _tenant.HasValue.Returns(true);
        _xmlSigner.Sign(Arg.Any<string>(), Arg.Any<X509Certificate2>())
            .Returns(new SignedXmlResult("<signed/>", "AbCdEfGhIj", "AbCdEf"));
        _vault.RetrieveAsync("vault-ref", Arg.Any<CancellationToken>())
            .Returns(_ => new CertificateSecret(TestPkcs12.Generate(), TestPkcs12.DefaultPassword));
    }

    private CertificateSigner Sut() => new(_tenant, _certificates, _vault, _xmlSigner, Clock);

    private Certificate ActiveCertificate()
        => Certificate.Issue(
            "101672919",
            DgiiEnvironment.Test,
            new CertificateDetails(
                "101672919", "CN=Test", "CN=Test", "ABC123",
                Clock.GetUtcNow().AddYears(-1), Clock.GetUtcNow().AddYears(1), true),
            "vault-ref",
            Clock.GetUtcNow()).Value;

    [Fact]
    public async Task Signs_with_the_active_certificate()
    {
        _certificates.GetActiveAsync(DgiiEnvironment.Test, Arg.Any<CancellationToken>())
            .Returns(ActiveCertificate());

        var result = await Sut().SignAsync(Xml, DgiiEnvironment.Test);

        result.IsError.ShouldBeFalse();
        result.Value.SecurityCode.ShouldBe("AbCdEf");
        _xmlSigner.Received(1).Sign(Xml, Arg.Any<X509Certificate2>());
        await _vault.Received(1).RetrieveAsync("vault-ref", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Fails_when_the_tenant_has_no_active_certificate()
    {
        _certificates.GetActiveAsync(Arg.Any<DgiiEnvironment>(), Arg.Any<CancellationToken>())
            .Returns((Certificate?)null);

        var result = await Sut().SignAsync(Xml, DgiiEnvironment.Test);

        result.FirstError.Code.ShouldBe("Certificate.NoActiveCertificate");
    }

    [Fact]
    public async Task Fails_when_the_certificate_is_revoked()
    {
        var certificate = ActiveCertificate();
        certificate.Revoke(Clock.GetUtcNow());
        _certificates.GetActiveAsync(Arg.Any<DgiiEnvironment>(), Arg.Any<CancellationToken>())
            .Returns(certificate);

        var result = await Sut().SignAsync(Xml, DgiiEnvironment.Test);

        result.FirstError.Code.ShouldBe("Certificate.NotUsable");
    }

    [Fact]
    public async Task Fails_when_the_request_has_no_tenant()
    {
        _tenant.HasValue.Returns(false);

        var result = await Sut().SignAsync(Xml, DgiiEnvironment.Test);

        result.FirstError.Code.ShouldBe("Auth.TenantNotResolved");
    }
}
