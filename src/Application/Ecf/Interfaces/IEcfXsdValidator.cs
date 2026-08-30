using ErrorOr;
using NovaFE.Domain.Common;

namespace NovaFE.Application.Ecf.Interfaces;

/// <summary>
/// Valida un XML de e-CF contra el XSD oficial de la DGII del tipo correspondiente
/// (RF-02.1). Los XSD van embebidos en el ensamblado de infraestructura.
/// </summary>
public interface IEcfXsdValidator
{
    /// <summary>
    /// Valida el XML <c>&lt;ECF&gt;</c> contra el XSD del <paramref name="type"/>.
    /// Devuelve <see cref="Result.Success"/> si valida, o un
    /// <see cref="Error.Validation"/> con el detalle de las violaciones.
    /// </summary>
    ErrorOr<Success> Validate(string xml, EcfType type);

    /// <summary>Valida el XML <c>&lt;RFCE&gt;</c> contra <c>RFCE-32-v1.0.xsd</c>.</summary>
    ErrorOr<Success> ValidateRfce(string xml);
}
