using ErrorOr;

namespace NovaFE.Domain.Ecf;

/// <summary>
/// Errores del ciclo de vida del comprobante emitido (Módulo 4). <c>code</c> en
/// inglés; descripción en español.
/// </summary>
public static class IssuedEcfErrors
{
    public static Error InvalidTransition(string from, string to) => Error.Conflict(
        code: "IssuedEcf.InvalidTransition",
        description: $"El comprobante está en estado '{from}' y no puede pasar a '{to}'.");

    public static Error NotRetriable(string status) => Error.Conflict(
        code: "IssuedEcf.NotRetriable",
        description: $"Solo se puede reintentar el envío de un comprobante en estado 'failed' o 'review'; este está en '{status}'.");
}
