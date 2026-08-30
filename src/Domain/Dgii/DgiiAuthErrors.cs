using ErrorOr;

namespace NovaFE.Domain.Dgii;

/// <summary>
/// Errores de la autenticación contra la DGII. Los fallos de red/timeout se
/// mapean con <c>HttpErrorMapper</c> a <c>Errors.Http.*</c>; estos son los
/// específicos del flujo semilla → token.
/// </summary>
public static class DgiiAuthErrors
{
    public static Error SeedRequestFailed(int statusCode) => Error.Failure(
        code: "Dgii.Auth.SeedRequestFailed",
        description: $"La DGII rechazó la solicitud de la semilla de autenticación (HTTP {statusCode}).");

    public static Error TokenRequestFailed(int statusCode) => Error.Failure(
        code: "Dgii.Auth.TokenRequestFailed",
        description: $"La DGII rechazó la validación de la semilla firmada (HTTP {statusCode}).");

    public static Error TokenRejected(string reason) => Error.Failure(
        code: "Dgii.Auth.TokenRejected",
        description: $"La DGII no emitió un token: {reason}");

    public static Error MalformedTokenResponse => Error.Failure(
        code: "Dgii.Auth.MalformedTokenResponse",
        description: "La respuesta de la DGII al validar la semilla no tiene el formato esperado.");
}
