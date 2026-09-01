using QRCoder;

namespace NovaFE.Infrastructure.Representation;

/// <summary>
/// Genera el PNG del timbre QR desde la URL de verificación de la DGII
/// (<see cref="Domain.Ecf.EcfVerificationUrl"/>). Corrección de error nivel M
/// (RF-09.3); la versión la elige QRCoder según la longitud — con las URLs de la
/// DGII (~200 caracteres) da 9 o más, por encima del mínimo 8.
/// </summary>
internal static class QrImage
{
    public static byte[] Png(string verificationUrl, int pixelsPerModule = 12)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(verificationUrl);

        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(verificationUrl, QRCodeGenerator.ECCLevel.M);
        var png = new PngByteQRCode(data);
        return png.GetGraphic(pixelsPerModule);
    }
}
