using ErrorOr;
using NovaFE.Domain.Common;
using NovaFE.Domain.Dgii;

namespace NovaFE.Application.Dgii;

/// <summary>
/// Cliente de bajo nivel del servicio de autenticación de la DGII (flujo de dos
/// pasos: semilla → validar semilla firmada → token). No cachea ni firma; eso lo
/// hace <see cref="IDgiiTokenProvider"/>.
/// </summary>
public interface IDgiiAuthClient
{
    /// <summary>
    /// <c>GET /{ambiente}/autenticacion/api/autenticacion/semilla</c> — devuelve
    /// el XML de la semilla tal cual, para firmarlo.
    /// </summary>
    Task<ErrorOr<string>> GetSeedAsync(DgiiEnvironment environment, CancellationToken ct = default);

    /// <summary>
    /// <c>POST /{ambiente}/autenticacion/api/autenticacion/validarsemilla</c>
    /// (multipart, campo <c>xml</c>) — devuelve el token ya mapeado a dominio.
    /// </summary>
    Task<ErrorOr<AuthenticationToken>> ValidateSeedAsync(
        DgiiEnvironment environment,
        string signedSeedXml,
        CancellationToken ct = default);
}
