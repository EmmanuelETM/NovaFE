using ErrorOr;

namespace NovaFE.Domain.Tenants;

/// <summary>
/// Errores de negocio del perfil fiscal del emisor. <c>code</c> en inglés (estable);
/// descripción en español (la consume quien llama a la API).
/// </summary>
public static class EmitterProfileErrors
{
    public static Error AddressRequired => Error.Validation(
        code: "EmitterProfile.AddressRequired",
        description: "La dirección del emisor es obligatoria: el e-CF la exige en el bloque Emisor.");

    public static Error TooManyPhones => Error.Validation(
        code: "EmitterProfile.TooManyPhones",
        description: $"El emisor admite hasta {EmitterProfile.MaxPhones} teléfonos.");

    public static Error NotConfigured => Error.Validation(
        code: "EmitterProfile.NotConfigured",
        description: "El contribuyente no tiene un perfil fiscal configurado (dirección, ambiente). El operador debe crearlo antes de emitir.");
}
