using NovaFE.Domain.Ecf;

namespace NovaFE.Application.Ecf.Interfaces;

/// <summary>
/// Serializa el <b>RFCE</b> (Resumen de Factura de Consumo Electrónica) — el formato
/// <i>reducido</i> que se envía a la DGII para el tipo 32 (Consumo) cuando el
/// <c>MontoTotal</c> es <b>&lt; DOP 250 000</b> (RF-02.6). El <c>&lt;ECF&gt;</c>
/// completo del tipo 32 se genera y se guarda localmente; el RFCE es su resumen:
/// solo el encabezado, los totales y un código de seguridad que lo ata al e-CF
/// original. No lleva detalle de líneas ni <c>&lt;FechaHoraFirma&gt;</c>.
/// </summary>
public interface IRfceSerializer
{
    /// <summary>
    /// Produce el XML <c>&lt;RFCE&gt;</c> a partir del e-CF tipo 32 completo.
    /// </summary>
    /// <param name="document">El e-CF tipo 32 del que este RFCE es resumen.</param>
    /// <param name="securityCode">
    /// <c>&lt;CodigoSeguridadeCF&gt;</c> — 6 caracteres, los primeros del
    /// <c>SignatureValue</c> del e-CF completo ya firmado (Módulo 3).
    /// </param>
    string Serialize(EcfDocument document, string securityCode);
}
