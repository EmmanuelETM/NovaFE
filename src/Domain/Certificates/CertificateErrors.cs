using ErrorOr;

namespace NovaFE.Domain.Certificates;

/// <summary>
/// Errores de negocio del módulo Certificates. <c>code</c> en inglés (estable);
/// descripción en español (la consume quien llama a la API).
/// </summary>
public static class CertificateErrors
{
    public static Error CannotOpen => Error.Validation(
        code: "Certificate.CannotOpen",
        description: "No se pudo abrir el certificado. Verifica que sea un archivo .p12/.pfx válido y que la contraseña sea correcta.");

    public static Error NoPrivateKey => Error.Validation(
        code: "Certificate.NoPrivateKey",
        description: "El certificado no contiene la clave privada, necesaria para firmar los e-CF.");

    public static Error Expired(DateTimeOffset validTo) => Error.Validation(
        code: "Certificate.Expired",
        description: $"El certificado venció el {validTo:dd-MM-yyyy}.");

    public static Error NotYetValid(DateTimeOffset validFrom) => Error.Validation(
        code: "Certificate.NotYetValid",
        description: $"El certificado es válido a partir del {validFrom:dd-MM-yyyy}.");

    public static Error RncMismatch(string certificateHolder, string tenantRnc) => Error.Validation(
        code: "Certificate.RncMismatch",
        description: $"El certificado pertenece al RNC/cédula '{certificateHolder}', que no coincide con el del contribuyente ('{tenantRnc}').");

    public static Error NotFound(Guid id) => Error.NotFound(
        code: "Certificate.NotFound",
        description: $"No existe un certificado con id '{id}'.");

    public static Error EnvironmentHasActiveCertificate(string environment) => Error.Conflict(
        code: "Certificate.EnvironmentHasActiveCertificate",
        description: $"El contribuyente ya tiene un certificado activo para el ambiente {environment}. Revócalo antes de subir otro.");

    public static Error AlreadyRevoked => Error.Conflict(
        code: "Certificate.AlreadyRevoked",
        description: "El certificado ya está revocado.");

    public static Error NoActiveCertificate(string environment) => Error.Failure(
        code: "Certificate.NoActiveCertificate",
        description: $"El contribuyente no tiene un certificado activo para el ambiente {environment}.");

    public static Error NotUsable(string environment) => Error.Failure(
        code: "Certificate.NotUsable",
        description: $"El certificado del ambiente {environment} no se puede usar: está revocado o fuera de su período de validez.");
}
