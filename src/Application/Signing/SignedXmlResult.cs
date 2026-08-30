namespace NovaFE.Application.Signing;

/// <summary>
/// Resultado de firmar un XML con XMLDSig.
/// </summary>
/// <param name="Xml">El documento firmado, con <c>&lt;Signature&gt;</c> como último hijo de la raíz.</param>
/// <param name="SignatureValue">El contenido Base64 de <c>&lt;SignatureValue&gt;</c>, tal cual aparece en el XML.</param>
/// <param name="SecurityCode">
/// Los primeros 6 caracteres de <see cref="SignatureValue"/>. Es el
/// <c>CodigoSeguridad</c> que va en el QR y la Representación Impresa
/// (RF-03.5 / RF-09.1 del plan técnico).
/// </param>
public sealed record SignedXmlResult(string Xml, string SignatureValue, string SecurityCode);
