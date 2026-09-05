using ErrorOr;
using NovaFE.Application.Tenants.CreateApiKey;
using NovaFE.Domain.Common;
using NovaFE.Domain.Tenants;

namespace NovaFE.UnitTests.Tenants;

public class ApiKeyTests
{
    private static readonly Guid TenantId = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private static ApiKey NewKey(
        string? label = "ERP",
        DgiiEnvironment? environment = null,
        ApiKeyRole? role = null,
        DateTimeOffset? expiresAt = null)
    {
        var env = environment ?? DgiiEnvironment.Test;
        var token = ApiKeyToken.Generate(env);
        return ApiKey.Create(
            TenantId, ApiKeyToken.Hash(token), ApiKeyToken.DisplayPrefix(token), label, env,
            role ?? ApiKeyRole.Emisor, expiresAt).Value;
    }

    [Theory]
    [InlineData("test")]
    [InlineData("cert")]
    [InlineData("prod")]
    public void Generated_token_carries_its_environment_slug(string slug)
    {
        var env = DgiiEnvironment.GetAll().Single(e => e.Slug == slug);
        var token = ApiKeyToken.Generate(env);

        token.ShouldStartWith($"sk_nfe_{slug}_");
        ApiKeyToken.LooksLikeToken(token).ShouldBeTrue();
        ApiKeyToken.Hash(token).ShouldBe(ApiKeyToken.Hash(token));
        ApiKeyToken.Hash(token).Length.ShouldBe(64);
        ApiKeyToken.DisplayPrefix(token).ShouldBe(token[..16]);
    }

    [Fact]
    public void Create_binds_the_environment()
    {
        NewKey(environment: DgiiEnvironment.Production).Environment.ShouldBe(DgiiEnvironment.Production);
    }

    [Fact]
    public void Create_binds_the_role()
    {
        NewKey(role: ApiKeyRole.Consultor).Role.ShouldBe(ApiKeyRole.Consultor);
    }

    [Fact]
    public void Create_rejects_a_blank_tenant()
    {
        var result = ApiKey.Create(
            Guid.Empty, "hash", "sk_nfe_test_x", "x", DgiiEnvironment.Test, ApiKeyRole.Emisor, null);

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("ApiKey.TenantRequired");
    }

    [Fact]
    public void Create_rejects_an_overlong_label()
    {
        var result = ApiKey.Create(
            TenantId, "hash", "sk_nfe_test_x", new string('x', ApiKey.MaxLabelLength + 1), DgiiEnvironment.Test,
            ApiKeyRole.Emisor, null);

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
