using Microsoft.Extensions.Options;

namespace NovaFE.Infrastructure.Security;

/// <summary>
/// Validación cruzada de <see cref="CertificateVaultOptions"/> al arrancar: cada
/// proveedor exige campos distintos, así que un <c>[Required]</c> fijo no sirve.
/// <list type="bullet">
/// <item><c>local</c> → <see cref="CertificateVaultOptions.MasterKey"/> presente,
/// base64 válido, exactamente 32 bytes.</item>
/// <item><c>azure-key-vault</c> →
/// <see cref="CertificateVaultOptions.KeyVaultKeyUri"/> presente y URI absoluta.</item>
/// </list>
/// Se registra con <c>ValidateOnStart</c>: una configuración incoherente impide el
/// arranque en lugar de fallar en la primera firma.
/// </summary>
internal sealed class CertificateVaultOptionsValidator : IValidateOptions<CertificateVaultOptions>
{
    public ValidateOptionsResult Validate(string? name, CertificateVaultOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var provider = options.Provider?.Trim().ToLowerInvariant();

        return provider switch
        {
            CertificateVaultOptions.ProviderLocal => ValidateLocal(options),
            CertificateVaultOptions.ProviderAzureKeyVault => ValidateAzureKeyVault(options),
            _ => ValidateOptionsResult.Fail(
                $"CertificateVault:Provider '{options.Provider}' no es válido. Usa "
                + $"'{CertificateVaultOptions.ProviderLocal}' o '{CertificateVaultOptions.ProviderAzureKeyVault}'."),
        };
    }

    private static ValidateOptionsResult ValidateLocal(CertificateVaultOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.MasterKey))
            return ValidateOptionsResult.Fail(
                "CertificateVault:MasterKey es obligatoria con Provider = local (32 bytes en base64).");

        byte[] key;
        try
        {
            key = Convert.FromBase64String(options.MasterKey);
        }
        catch (FormatException)
        {
            return ValidateOptionsResult.Fail("CertificateVault:MasterKey no es base64 válido.");
        }

        return key.Length == 32
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(
                "CertificateVault:MasterKey debe ser exactamente 32 bytes (AES-256).");
    }

    private static ValidateOptionsResult ValidateAzureKeyVault(CertificateVaultOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.KeyVaultKeyUri))
            return ValidateOptionsResult.Fail(
                "CertificateVault:KeyVaultKeyUri es obligatoria con Provider = azure-key-vault.");

        return Uri.TryCreate(options.KeyVaultKeyUri, UriKind.Absolute, out _)
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(
                "CertificateVault:KeyVaultKeyUri debe ser una URI absoluta, p. ej. "
                + "https://<vault>.vault.azure.net/keys/<clave>.");
    }
}
