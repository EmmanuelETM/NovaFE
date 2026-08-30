using System.Globalization;
using ErrorOr;

namespace NovaFE.Domain.Sequences;

/// <summary>
/// Errores de negocio del módulo de secuencias e-NCF. Los <c>code</c> son
/// identificadores estables (inglés); las descripciones las consume quien llama a
/// la API, por eso van en español.
/// </summary>
public static class SequenceErrors
{
    public static Error MalformedEncf(string value) => Error.Validation(
        code: "Sequence.MalformedEncf",
        description: $"El e-NCF '{value}' no tiene el formato esperado: serie (E–Z, sin la P), dos dígitos de tipo y diez de secuencial.");

    public static Error InvalidSeries(char series) => Error.Validation(
        code: "Sequence.InvalidSeries",
        description: $"La serie '{series}' no es válida. Debe ser una letra de la E a la Z, excepto la P.");

    public static Error InvalidRange(long rangeFrom, long rangeTo) => Error.Validation(
        code: "Sequence.InvalidRange",
        description: $"El rango [{rangeFrom.ToString(CultureInfo.InvariantCulture)}, {rangeTo.ToString(CultureInfo.InvariantCulture)}] no es válido: 'desde' debe ser mayor o igual a 1 y menor o igual a 'hasta'.");

    public static Error CertEcfMustStartAtOne => Error.Validation(
        code: "Sequence.CertEcfMustStartAtOne",
        description: "En CerteCF las secuencias siempre empiezan en 1.");

    public static Error CertEcfRangeTooLarge(long maximum) => Error.Validation(
        code: "Sequence.CertEcfRangeTooLarge",
        description: $"En CerteCF el rango por tipo no puede exceder {maximum.ToString("N0", CultureInfo.InvariantCulture)} secuencias.");

    public static Error UnknownType(int code) => Error.Validation(
        code: "Sequence.UnknownType",
        description: $"El tipo de comprobante '{code}' no existe. Valores válidos: 31, 32, 33, 34, 41, 43, 44, 45, 46, 47.");

    public static Error SeriesAlreadyActive(char series, string type) => Error.Conflict(
        code: "Sequence.SeriesAlreadyActive",
        description: $"Ya hay un rango activo para la serie '{series}' del tipo {type} en este ambiente. Desactívalo antes de registrar otro.");

    public static Error NotFound(Guid id) => Error.NotFound(
        code: "Sequence.NotFound",
        description: $"No existe un rango de secuencias con id '{id}'.");

    public static Error RangeInactive => Error.Conflict(
        code: "Sequence.RangeInactive",
        description: "El rango de secuencias está inactivo.");

    public static Error RangeExpired(DateOnly expiresOn) => Error.Conflict(
        code: "Sequence.RangeExpired",
        description: $"El rango de secuencias venció el {expiresOn.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture)}.");

    public static Error RangeExhausted => Error.Conflict(
        code: "Sequence.RangeExhausted",
        description: "El rango de secuencias se agotó. Registra un nuevo rango autorizado por la DGII.");

    public static Error NoAuthorizedRange(string type) => Error.Failure(
        code: "Sequence.NoAuthorizedRange",
        description: $"El contribuyente no tiene un rango de secuencias autorizado para el tipo {type} en este ambiente.");

    public static Error AllRangesExpired(string type) => Error.Failure(
        code: "Sequence.AllRangesExpired",
        description: $"Todos los rangos de secuencias del tipo {type} en este ambiente están vencidos.");

    public static Error AllRangesExhausted(string type) => Error.Failure(
        code: "Sequence.AllRangesExhausted",
        description: $"Se agotaron todos los rangos de secuencias del tipo {type} en este ambiente.");
}
