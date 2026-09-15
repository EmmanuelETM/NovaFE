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
    /// DGII por <paramref name="dgii"/>. <paramref name="signedDuringContingency"/>
    /// tampoco está en el XML (M11 Tipo 1 no lo toca, ver
    /// <c>docs/contingency.md</c>) — viene de <c>issued_ecf.signed_during_contingency</c>
    /// y, si es <c>true</c>, arma la leyenda verbatim en <see cref="RepresentationModel.ContingencyNotice"/>.
    /// </summary>
    RepresentationModel Read(
        string signedEcfXml,
        RepresentationVerification verification,
        RepresentationDgiiStatus? dgii,
        bool signedDuringContingency = false);
}
