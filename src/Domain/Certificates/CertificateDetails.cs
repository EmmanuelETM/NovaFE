namespace NovaFE.Domain.Certificates;

/// <summary>
/// Datos extraídos de un archivo PKCS#12, ya inspeccionado. Es la entrada para
/// <see cref="Certificate.Issue"/>; no contiene la clave privada ni los bytes.
/// </summary>
/// <param name="HolderIdentifier">
/// El componente <c>SERIALNUMBER</c> (OID 2.5.4.5) del Subject. En los
/// certificados INDOTEL dominicanos aquí va el RNC o la cédula del titular; la
/// DGII exige que coincida con el emisor antes de firmar.
/// </param>
public sealed record CertificateDetails(
    string HolderIdentifier,
    string Subject,
    string Issuer,
    string Thumbprint,
    DateTimeOffset ValidFrom,
    DateTimeOffset ValidTo,
    bool HasPrivateKey);
