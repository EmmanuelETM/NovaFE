using NovaFE.Domain.Common;

namespace NovaFE.Domain.Ecf;

/// <summary>
/// Estado del comprobante en el ciclo de vida de NovaFE (contrato público de la
/// API). Se persiste por <see cref="Enumeration{T}.Name"/>; la API lo expone con
/// <see cref="PublicName"/>.
/// <para>
/// v1 (Módulo 12) solo llega hasta <see cref="Signed"/>. Los estados de envío a la
/// DGII (<c>submitted</c>, <c>accepted</c>, <c>rejected</c>, <c>processing</c>,
/// <c>accepted_conditional</c>, <c>contingency</c>, <c>voided</c>, <c>failed</c>)
/// llegan con Módulo 4.
/// </para>
/// </summary>
public sealed record EcfStatus(int Id, string Name, string PublicName) : Enumeration<EcfStatus>(Id, Name)
{
    /// <summary>Secuencia asignada, XML armado, cuadrado y firmado. Aún no enviado a la DGII.</summary>
    public static readonly EcfStatus Signed = new(1, nameof(Signed), "signed");
}
