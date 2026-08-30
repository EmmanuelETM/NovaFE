using ErrorOr;
using NovaFE.Domain.Common;
using NovaFE.Domain.Dgii;

namespace NovaFE.Application.Dgii;

/// <summary>
/// Da un token de la DGII válido para el <b>tenant actual</b> en un ambiente:
/// mira la caché, y si no hay o está por vencer, corre el flujo semilla → firma
/// → validar y lo guarda. Renovación proactiva (RF-01.3). Es lo que usan el
/// gateway de e-CF y la emisión.
/// </summary>
public interface IDgiiTokenProvider
{
    Task<ErrorOr<AuthenticationToken>> GetTokenAsync(DgiiEnvironment environment, CancellationToken ct = default);
}
