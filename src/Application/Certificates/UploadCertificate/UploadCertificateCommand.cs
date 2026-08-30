namespace NovaFE.Application.Certificates.UploadCertificate;

/// <summary>
/// Sube el certificado digital (PKCS#12) del contribuyente para un ambiente de la
/// DGII. Devuelve el id del certificado registrado.
/// </summary>
public sealed record UploadCertificateCommand(byte[] Content, string Password, string Environment);
