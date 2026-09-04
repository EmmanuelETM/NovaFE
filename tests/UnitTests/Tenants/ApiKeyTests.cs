using ErrorOr;
using NovaFE.Application.Tenants.CreateApiKey;
using NovaFE.Domain.Tenants;

namespace NovaFE.UnitTests.Tenants;

public class ApiKeyTests
{
    private static readonly Guid TenantId = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private static ApiKey NewKey(string? label = "ERP", DateTimeOffset? expiresAt = null)
    {
        var token = ApiKeyToken.Generate();
        return ApiKey.Create(TenantId, ApiKeyToken.Hash(token), ApiKeyToken.DisplayPrefix(token), label, expiresAt).Value;
    }

    [Fact]
    public void Generated_token_is_prefixed_and_hashes_stably()
    {
        var token = ApiKeyToken.Generate();

        token.ShouldStartWith("nfe_");
        ApiKeyToken.LooksLikeToken(token).ShouldBeTrue();
        ApiKeyToken.Hash(token).ShouldBe(ApiKeyToken.Hash(token));
        ApiKeyToken.Hash(token).Length.ShouldBe(64);
        ApiKeyToken.DisplayPrefix(token).ShouldBe(token[..12]);
    }

    [Fact]
    public void Create_rejects_a_blank_tenant()
    {
        var result = ApiKey.Create(Guid.Empty, "hash", "nfe_abc", "x", null);

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("ApiKey.TenantRequired");
    }

    [Fact]
    public void Create_rejects_an_overlong_label()
    {
        var result = ApiKey.Create(TenantId, "hash", "nfe_abc", new string('x', ApiKey.MaxLabelLength + 1), null);

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("ApiKey.LabelTooLong");
    }

    [Fact]
    public void Create_defaults_a_missing_label()
    {
        NewKey(label: null).Label.ShouldBe("Sin etiqueta");
        NewKey(label: "  ").Label.ShouldBe("Sin etiqueta");
    }

    [Fact]
    public void A_fresh_key_is_usable()
    {
        NewKey().IsUsableAt(Now).ShouldBeTrue();
    }

    [Fact]
    public void An_expired_key_is_not_usable()
    {
        var key = NewKey(expiresAt: Now.AddHours(-1));

        key.IsUsableAt(Now).ShouldBeFalse();
        key.IsUsableAt(Now.AddHours(-2)).ShouldBeTrue();
    }

    [Fact]
    public void Revoking_makes_it_unusable_and_is_not_idempotent()
    {
        var key = NewKey();

        key.Revoke(Now).IsError.ShouldBeFalse();
        key.RevokedAt.ShouldBe(Now);
        key.IsUsableAt(Now).ShouldBeFalse();

        var second = key.Revoke(Now.AddMinutes(1));
        second.IsError.ShouldBeTrue();
        second.FirstError.Type.ShouldBe(ErrorType.Conflict);
    }

    [Fact]
    public void MarkUsed_records_the_last_use()
    {
        var key = NewKey();

        key.MarkUsed(Now);

        key.LastUsedAt.ShouldBe(Now);
    }
}
