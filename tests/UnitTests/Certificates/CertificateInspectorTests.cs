using NovaFE.Domain.Certificates;

namespace NovaFE.UnitTests.Certificates;

public class CertificateInspectorTests
{
    [Fact]
    public void Inspect_extracts_the_holder_identifier_and_validity()
    {
        var from = DateTimeOffset.UtcNow.AddDays(-2);
        var to = DateTimeOffset.UtcNow.AddYears(1);
        var pkcs12 = TestPkcs12.Generate(holderIdentifier: "130862346", notBefore: from, notAfter: to);

        var result = CertificateInspector.Inspect(pkcs12, TestPkcs12.DefaultPassword);

        result.IsError.ShouldBeFalse();
        result.Value.HolderIdentifier.ShouldBe("130862346");
        result.Value.HasPrivateKey.ShouldBeTrue();
        result.Value.ValidFrom.ShouldBe(from, TimeSpan.FromSeconds(2));
        result.Value.ValidTo.ShouldBe(to, TimeSpan.FromSeconds(2));
        result.Value.Thumbprint.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Inspect_reports_a_certificate_without_a_private_key()
    {
        var pkcs12 = TestPkcs12.Generate(withPrivateKey: false);

        var result = CertificateInspector.Inspect(pkcs12, TestPkcs12.DefaultPassword);

        result.IsError.ShouldBeFalse();
        result.Value.HasPrivateKey.ShouldBeFalse();
    }

    [Fact]
    public void Inspect_fails_on_a_wrong_password()
    {
        var pkcs12 = TestPkcs12.Generate();

        var result = CertificateInspector.Inspect(pkcs12, "not-the-password");

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("Certificate.CannotOpen");
    }

    [Fact]
    public void Inspect_fails_on_garbage_bytes()
    {
        var result = CertificateInspector.Inspect([1, 2, 3, 4, 5], "whatever");

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("Certificate.CannotOpen");
    }
}
