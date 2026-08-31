using ErrorOr;

namespace NovaFE.Domain.Dgii;

/// <summary>
/// Errores del envío de e-CF a la DGII y de la consulta de resultado (Módulo 4).
/// Los fallos de red/timeout se mapean con <c>HttpErrorMapper</c> a
/// <c>Errors.Http.*</c> (los reintenta el outbox); estos son los específicos del
/// gateway de recepción.
/// </summary>
public static class DgiiSubmissionErrors
{
    public static Error ReceptionFailed(int statusCode) => Error.Failure(
        code: "Dgii.Submission.ReceptionFailed",
        description: $"La DGII rechazó la recepción del comprobante (HTTP {statusCode}).");

    public static Error NoTrackId(string? detail) => Error.Failure(
        code: "Dgii.Submission.NoTrackId",
        description: string.IsNullOrWhiteSpace(detail)
            ? "La DGII aceptó la recepción pero no devolvió un TrackId."
            : $"La DGII no devolvió un TrackId: {detail}");

    public static Error MalformedResponse => Error.Failure(
        code: "Dgii.Submission.MalformedResponse",
        description: "La respuesta de la DGII no tiene el formato esperado.");

    public static Error ResultQueryFailed(int statusCode) => Error.Failure(
        code: "Dgii.Submission.ResultQueryFailed",
        description: $"La consulta de resultado a la DGII falló (HTTP {statusCode}).");
}
