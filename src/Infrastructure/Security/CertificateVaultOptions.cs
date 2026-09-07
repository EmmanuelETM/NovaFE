namespace NovaFE.Infrastructure.Security;

/// <summary>
/// Configuración del vault de certificados. La clave maestra (KEK) protege las
/// claves de datos con que se cifra cada PKCS#12.
/// <para>
/// <see cref="Provider"/> elige la implementación de <c>IKeyProtector</c>:
/// <c>local</c> (KEK en <see cref="MasterKey"/>, por defecto) o
/// <c>azure-key-vault</c> (la KEK vive en Azure Key Vault; se envuelve/desenvuelve
/// la DEK con una clave RSA del vault, sin que la KEK toque nunca la app). Cambiar
/// de proveedor es solo esta configuración. Ver <c>docs/certificates.md</c>.
/// </para>
/// </summary>
public sealed class CertificateVaultOptions
{
    public const string SectionName = "CertificateVault";

    public const string ProviderLocal = "local";
    public const string ProviderAzureKeyVault = "azure-key-vault";

    /// <summary>
    /// Proveedor de <c>IKeyProtector</c>: <c>local</c> (por defecto) o
    /// <c>azure-key-vault</c>. La validación cruzada la hace
    /// <see cref="CertificateVaultOptionsValidator"/> al arrancar.
    /// </summary>
    public string Provider { get; set; } = ProviderLocal;

    /// <summary>
    /// KEK en base64, exactamente 32 bytes (AES-256). Requerida solo con
    /// <c>Provider = local</c>. En desarrollo va en user-secrets o
    /// <c>appsettings.Development.json</c>; en producción, una variable de entorno.
    /// </summary>
    public string MasterKey { get; set; } = string.Empty;

    /// <summary>
    /// URI completa a la clave RSA del Key Vault, p. ej.
    /// <c>https://novafe-kv.vault.azure.net/keys/cert-kek</c> (o con versión al
    /// final). Requerida solo con <c>Provider = azure-key-vault</c>. La credencial
    /// la resuelve <c>DefaultAzureCredential</c> (managed identity en Azure).
    /// </summary>
    public string KeyVaultKeyUri { get; set; } = string.Empty;
}
