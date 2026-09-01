namespace NovaFE.Application.Ecf.Representation;

/// <summary>
/// Lee el <c>&lt;ECF&gt;</c> firmado y lo proyecta a un <see cref="RepresentationModel"/>
/// para la Representación Impresa. Es cripto-ignorante: se salta el bloque
/// <c>&lt;Signature&gt;</c> y solo mira los datos del comprobante.
/// </summary>
public interface IEcfRepresentationReader
{
    /// <summary>
    /// Proyecta <paramref name="signedEcfXml"/> (el <c>&lt;ECF&gt;</c> completo, no
    /// el RFCE) al modelo de la RI. El código de seguridad y la URL del QR no
    /// están en el XML — entran por <paramref name="verification"/>; el estado
    /// DGII por <paramref name="dgii"/>.
    /// </summary>
    RepresentationModel Read(
        string signedEcfXml,
        RepresentationVerification verification,
        RepresentationDgiiStatus? dgii);
}
