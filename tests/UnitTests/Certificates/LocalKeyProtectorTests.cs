using System.Security.Cryptography;
using NovaFE.Infrastructure.Security;
using Microsoft.Extensions.Options;

namespace NovaFE.UnitTests.Certificates;

public class LocalKeyProtectorTests
{
    private static LocalKeyProtector Create(string? masterKey = null)
        => new(Options.Create(new CertificateVaultOptions
        {
            MasterKey = masterKey ?? Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
        }));

    [Fact]
    public async Task Wrap_then_unwrap_returns_the_original_data_key()
    {
        var protector = Create();
        var dataKey = RandomNumberGenerator.GetBytes(32);

        var wrapped = await protector.WrapAsync(dataKey);
        var unwrapped = await protector.UnwrapAsync(wrapped);

        unwrapped.ShouldBe(dataKey);
        wrapped.ShouldNotBe(dataKey);
    }

    [Fact]
    public async Task Unwrap_fails_when_the_blob_was_tampered()
    {
        var protector = Create();
        var wrapped = await protector.WrapAsync(RandomNumberGenerator.GetBytes(32));
        wrapped[^1] ^= 0xFF;

        await Should.ThrowAsync<CryptographicException>(() => protector.UnwrapAsync(wrapped));
    }

    [Fact]
    public async Task Unwrap_fails_with_a_different_master_key()
    {
        var wrapped = await Create().WrapAsync(RandomNumberGenerator.GetBytes(32));

        await Should.ThrowAsync<CryptographicException>(() => Create().UnwrapAsync(wrapped));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-base64!!")]
    [InlineData("dG9vLXNob3J0")] // valid base64 but < 32 bytes
    public void Ctor_rejects_a_bad_master_key(string masterKey)
        => Should.Throw<InvalidOperationException>(() => Create(masterKey));
}
