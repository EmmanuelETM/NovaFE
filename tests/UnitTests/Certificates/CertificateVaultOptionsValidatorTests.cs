using System.Security.Cryptography;
using NovaFE.Infrastructure.Security;

namespace NovaFE.UnitTests.Certificates;

public class CertificateVaultOptionsValidatorTests
{
    private readonly CertificateVaultOptionsValidator _validator = new();

    private static string ValidMasterKey() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    [Fact]
    public void Local_with_a_valid_master_key_succeeds()
    {
        var result = _validator.Validate(null, new CertificateVaultOptions
        {
            Provider = CertificateVaultOptions.ProviderLocal,
            MasterKey = ValidMasterKey(),
        });

        result.Succeeded.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-base64!!")]
    [InlineData("dG9vLXNob3J0")] // valid base64 but < 32 bytes
    public void Local_with_a_bad_master_key_fails(string masterKey)
    {
        var result = _validator.Validate(null, new CertificateVaultOptions
        {
            Provider = CertificateVaultOptions.ProviderLocal,
            MasterKey = masterKey,
        });

        result.Failed.ShouldBeTrue();
    }

    [Fact]
    public void Local_ignores_a_missing_key_vault_uri()
    {
        var result = _validator.Validate(null, new CertificateVaultOptions
        {
            Provider = CertificateVaultOptions.ProviderLocal,
            MasterKey = ValidMasterKey(),
            KeyVaultKeyUri = string.Empty,
        });

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Azure_key_vault_with_an_absolute_uri_succeeds_even_without_a_master_key()
    {
        var result = _validator.Validate(null, new CertificateVaultOptions
        {
            Provider = CertificateVaultOptions.ProviderAzureKeyVault,
            MasterKey = string.Empty,
            KeyVaultKeyUri = "https://novafe-kv.vault.azure.net/keys/cert-kek",
        });

        result.Succeeded.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("keys/cert-kek")] // relative
    public void Azure_key_vault_without_a_valid_uri_fails(string uri)
    {
        var result = _validator.Validate(null, new CertificateVaultOptions
        {
            Provider = CertificateVaultOptions.ProviderAzureKeyVault,
            KeyVaultKeyUri = uri,
        });

        result.Failed.ShouldBeTrue();
    }

    [Theory]
    [InlineData("supabase-vault")]
    [InlineData("")]
    [InlineData("aws-kms")]
    public void An_unknown_provider_fails(string provider)
    {
        var result = _validator.Validate(null, new CertificateVaultOptions
        {
            Provider = provider,
            MasterKey = ValidMasterKey(),
        });

        result.Failed.ShouldBeTrue();
    }

    [Fact]
    public void The_provider_match_is_case_and_whitespace_insensitive()
    {
        var result = _validator.Validate(null, new CertificateVaultOptions
        {
            Provider = "  LOCAL  ",
            MasterKey = ValidMasterKey(),
        });

        result.Succeeded.ShouldBeTrue();
    }
}
