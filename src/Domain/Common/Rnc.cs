using ErrorOr;

namespace NovaFE.Domain.Common;

/// <summary>
/// Registro Nacional del Contribuyente — el identificador tributario de la DGII.
/// Entre 9 y 11 dígitos, sin separadores. Es la identidad de emisores y
/// compradores a lo largo de todo el dominio e-CF.
/// </summary>
public readonly record struct Rnc
{
    private Rnc(string value) => Value = value;

    public string Value { get; }

    /// <summary>
    /// Validates and normalizes raw input. Returns a validation error instead of
    /// throwing so callers can surface it through <c>ErrorOr</c>.
    /// </summary>
    public static ErrorOr<Rnc> Create(string? raw)
    {
        var normalized = (raw ?? string.Empty).Trim();

        if (normalized.Length == 0)
            return Errors.Validation.Required("RNC");

        if (!IsWellFormed(normalized))
            return Errors.Validation.Invalid("RNC");

        return new Rnc(normalized);
    }

    /// <summary>
    /// Rehydrates a value that was already validated on the way into storage.
    /// Only the persistence layer should call this.
    /// </summary>
    public static Rnc FromStorage(string value) => new(value);

    public static bool IsWellFormed(string value)
        => value.Length is >= 9 and <= 11 && value.All(char.IsAsciiDigit);

    public override string ToString() => Value;

    public static implicit operator string(Rnc rnc) => rnc.Value;
}
