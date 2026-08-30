using System.ComponentModel.DataAnnotations;

namespace NovaFE.Infrastructure.Security;

/// <summary>
/// Configuración del vault de certificados. La clave maestra (KEK) protege las
/// claves de datos con que se cifra cada PKCS#12.
/// </summary>
public sealed class CertificateVaultOptions
{
    public const string SectionName = "CertificateVault";

    /// <summary>
    /// KEK en base64, exactamente 32 bytes (AES-256). En desarrollo va en
    /// user-secrets o <c>appsettings.Development.json</c>; en producción, una
    /// variable de entorno o un KMS. Ver <c>docs/certificates.md</c>.
    /// </summary>
    [Required(AllowEmptyStrings = false,
        ErrorMessage = "Falta CertificateVault:MasterKey (32 bytes en base64).")]
    public string MasterKey { get; set; } = string.Empty;
}
