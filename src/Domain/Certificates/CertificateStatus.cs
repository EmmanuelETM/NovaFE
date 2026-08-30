using NovaFE.Domain.Common;

namespace NovaFE.Domain.Certificates;

/// <summary>
/// Estado de un <see cref="Certificate"/>. El vencimiento no es un estado
/// almacenado: se deriva de la ventana de validez para que no quede desfasado.
/// </summary>
public sealed record CertificateStatus(int Id, string Name) : Enumeration<CertificateStatus>(Id, Name)
{
    /// <summary>Vigente y utilizable para firmar (si además está dentro de su ventana de validez).</summary>
    public static readonly CertificateStatus Active = new(1, nameof(Active));

    /// <summary>Revocado por el tenant o el operador. No se usa aunque no haya vencido.</summary>
    public static readonly CertificateStatus Revoked = new(2, nameof(Revoked));
}
