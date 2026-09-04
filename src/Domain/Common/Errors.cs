using ErrorOr;

namespace NovaFE.Domain.Common;

/// <summary>
/// Errores transversales del sistema. Errores de negocio específicos
/// van en su propio archivo dentro del módulo correspondiente.
/// Ejemplo: Domain/Auth/Errors/AuthErrors.cs
/// </summary>
public static partial class Errors
{
    public static class General
    {
        public static Error Unexpected => Error.Unexpected(
            code: "General.Unexpected",
            description: "Ocurrió un error inesperado.");

        public static Error DatabaseError => Error.Failure(
            code: "General.DatabaseError",
            description: "Ocurrió un error al acceder a la base de datos.");

        public static Error NotFound => Error.NotFound(
            code: "General.NotFound",
            description: "El recurso solicitado no fue encontrado.");
    }

    public static class Validation
    {
        public static Error Required(string field) => Error.Validation(
            code: "Validation.Required",
            description: $"El campo '{field}' es requerido.");

        public static Error Invalid(string field) => Error.Validation(
            code: "Validation.Invalid",
            description: $"El valor del campo '{field}' no es válido.");
    }

    public static class Auth
    {
        public static Error TenantNotResolved => Error.Validation(
            code: "Auth.TenantNotResolved",
            description: "La petición no identifica un contribuyente.");

        public static Error MissingApiKey => Error.Unauthorized(
            code: "Auth.MissingApiKey",
            description: "La petición no trae credencial. Incluye el header X-API-Key.");

        public static Error InvalidApiKey => Error.Unauthorized(
            code: "Auth.InvalidApiKey",
            description: "La credencial de la petición no es válida, está revocada o venció.");
    }

    public static class Http
    {
        public static Error Timeout => Error.Failure(
            code: "Http.Timeout",
            description: "La solicitud al servicio externo excedió el tiempo de espera.");

        public static Error Unreachable => Error.Failure(
            code: "Http.Unreachable",
            description: "No se puede establecer conexión con el servicio externo.");

        public static Error CircuitOpen => Error.Failure(
            code: "Http.CircuitOpen",
            description: "El servicio externo no está disponible. Intente nuevamente más tarde.");

        public static Error RequestFailed => Error.Failure(
            code: "Http.RequestFailed",
            description: "El servicio externo retornó una respuesta de error.");
    }
}