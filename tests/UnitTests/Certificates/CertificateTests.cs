using NovaFE.Domain.Certificates;
using NovaFE.Domain.Common;

namespace NovaFE.UnitTests.Certificates;

public class CertificateTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);

    private static CertificateDetails Details(
        string holder = "101672919",
        bool hasKey = true,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null)
        => new(
            HolderIdentifier: holder,
            Subject: "CN=Test, SERIALNUMBER=" + holder,
            Issuer: "CN=Test",
            Thumbprint: "AABBCC",
            ValidFrom: from ?? Now.AddYears(-1),
            ValidTo: to ?? Now.AddYears(1),
            HasPrivateKey: hasKey);

    [Fact]
    public void Issue_succeeds_when_everything_matches()
    {
        var result = Certificate.Issue("101672919", DgiiEnvironment.Test, Details(), "vault-ref", Now);

        result.IsError.ShouldBeFalse();
        result.Value.Status.ShouldBe(CertificateStatus.Active);
        result.Value.Environment.ShouldBe(DgiiEnvironment.Test);
        result.Value.VaultReference.ShouldBe("vault-ref");
        result.Value.IsUsable(Now).ShouldBeTrue();
    }

    [Fact]
    public void Issue_matches_the_rnc_ignoring_prefixes_and_separators()
    {
        var result = Certificate.Issue("101672919", DgiiEnvironment.Production, Details(holder: "RNC-1-0167-2919"), "ref", Now);

        result.IsError.ShouldBeFalse();
    }

    [Fact]
    public void Issue_rejects_a_certificate_without_a_private_key()
        => Certificate.Issue("101672919", DgiiEnvironment.Test, Details(hasKey: false), "ref", Now)
            .FirstError.Code.ShouldBe("Certificate.NoPrivateKey");

    [Fact]
    public void Issue_rejects_an_expired_certificate()
        => Certificate.Issue("101672919", DgiiEnvironment.Test, Details(to: Now.AddDays(-1)), "ref", Now)
            .FirstError.Code.ShouldBe("Certificate.Expired");

    [Fact]
    public void Issue_rejects_a_not_yet_valid_certificate()
        => Certificate.Issue("101672919", DgiiEnvironment.Test, Details(from: Now.AddDays(1)), "ref", Now)
            .FirstError.Code.ShouldBe("Certificate.NotYetValid");

    [Fact]
    public void Issue_rejects_a_holder_that_is_not_the_tenant_rnc()
        => Certificate.Issue("101672919", DgiiEnvironment.Test, Details(holder: "130000001"), "ref", Now)
            .FirstError.Code.ShouldBe("Certificate.RncMismatch");

    [Fact]
    public void Revoke_moves_status_and_is_idempotent_only_once()
    {
        var certificate = Certificate.Issue("101672919", DgiiEnvironment.Test, Details(), "ref", Now).Value;

        certificate.Revoke(Now).IsError.ShouldBeFalse();
        certificate.Status.ShouldBe(CertificateStatus.Revoked);
        certificate.RevokedAt.ShouldBe(Now);
        certificate.IsUsable(Now).ShouldBeFalse();

        certificate.Revoke(Now).FirstError.Code.ShouldBe("Certificate.AlreadyRevoked");
    }

    [Fact]
    public void IsUsable_is_false_outside_the_validity_window()
    {
        var certificate = Certificate.Issue(
            "101672919", DgiiEnvironment.Test, Details(from: Now.AddYears(-2), to: Now.AddDays(10)), "ref", Now).Value;

        certificate.IsUsable(Now.AddDays(20)).ShouldBeFalse();
    }
}
