using System.Globalization;
using ErrorOr;
using NovaFE.Domain.Common;

namespace NovaFE.Domain.Sequences;

/// <summary>
/// Número de Comprobante Fiscal Electrónico. Trece caracteres:
/// <c>[Serie E–Z, excl. P] + [Tipo, 2 dígitos] + [Secuencial, 10 dígitos]</c>.
/// Ejemplo: <c>E310000000001</c>.
/// </summary>
public readonly record struct Encf
{
    /// <summary>Longitud fija del e-NCF.</summary>
    public const int Length = 13;

    private Encf(char series, int typeCode, long sequential)
    {
        Series = series;
        TypeCode = typeCode;
        Sequential = sequential;
    }

    /// <summary>Serie del rango autorizado (una letra de la E a la Z, sin la P).</summary>
    public char Series { get; }

    /// <summary>Código de dos dígitos del <see cref="EcfType"/>.</summary>
    public int TypeCode { get; }

    /// <summary>Posición dentro del rango autorizado (1 a 9 999 999 999).</summary>
    public long Sequential { get; }

    /// <summary>El tipo de comprobante al que pertenece este e-NCF.</summary>
    public EcfType Type => EcfType.FromValue(TypeCode);

    /// <summary>Representación de trece caracteres.</summary>
    public string Value =>
        string.Create(CultureInfo.InvariantCulture, $"{Series}{TypeCode:D2}{Sequential:D10}");

    /// <summary>Una serie es válida si es una letra de la E a la Z distinta de la P.</summary>
    public static bool IsValidSeries(char series) => series is >= 'E' and <= 'Z' and not 'P';

    /// <summary>
    /// Valida y normaliza una cadena. Devuelve un error de validación en vez de
    /// lanzar para que quien llama lo propague por <c>ErrorOr</c>.
    /// </summary>
    public static ErrorOr<Encf> Create(string? raw)
    {
        var normalized = (raw ?? string.Empty).Trim().ToUpperInvariant();

        if (normalized.Length == 0)
            return Errors.Validation.Required("e-NCF");

        if (normalized.Length != Length)
            return SequenceErrors.MalformedEncf(normalized);

        var series = normalized[0];
        if (!IsValidSeries(series))
            return SequenceErrors.MalformedEncf(normalized);

        if (!int.TryParse(normalized.AsSpan(1, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var typeCode)
            || EcfType.FromCodeOrDefault(typeCode) is null)
            return SequenceErrors.MalformedEncf(normalized);

        if (!long.TryParse(normalized.AsSpan(3, 10), NumberStyles.None, CultureInfo.InvariantCulture, out var sequential)
            || sequential < 1)
            return SequenceErrors.MalformedEncf(normalized);

        return new Encf(series, typeCode, sequential);
    }

    /// <summary>Construye un e-NCF a partir de partes ya validadas por el dominio.</summary>
    public static Encf Build(char series, int typeCode, long sequential) =>
        new(series, typeCode, sequential);

    /// <summary>
    /// Rehidrata un valor que ya se validó al entrar a almacenamiento. Solo la capa
    /// de persistencia debería llamarlo.
    /// </summary>
    public static Encf FromStorage(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        return new Encf(
            value[0],
            int.Parse(value.AsSpan(1, 2), CultureInfo.InvariantCulture),
            long.Parse(value.AsSpan(3, 10), CultureInfo.InvariantCulture));
    }

    public override string ToString() => Value;

    public static implicit operator string(Encf encf) => encf.Value;
}
