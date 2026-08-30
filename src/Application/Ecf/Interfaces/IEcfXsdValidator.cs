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
    /// Devuelve <see cref="Result.Success"/> si el XML valida, o un
    /// <see cref="Error.Validation"/> con el detalle de las violaciones.
    /// </summary>
    ErrorOr<Success> Validate(string xml, EcfType type);
}
