using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NovaFE.Domain.Common.Json;

/// <summary>
/// La API expone y acepta las fechas de calendario (<see cref="DateOnly"/>) en el
/// formato de la DGII, <c>dd-MM-yyyy</c> — igual que los campos de documento del
/// e-CF (<c>FechaEmision</c>, <c>FechaVencimientoSecuencia</c>…). Al leer también
/// acepta ISO <c>yyyy-MM-dd</c> por tolerancia.
/// </summary>
public sealed class DateOnlyJsonConverter : JsonConverter<DateOnly>
{
    private const string Format = "dd-MM-yyyy";

    private static readonly string[] AcceptedFormats = [Format, "yyyy-MM-dd"];

    public override DateOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var text = reader.GetString();

        if (string.IsNullOrWhiteSpace(text))
            throw new JsonException("La fecha no puede estar vacía.");

        if (DateOnly.TryParseExact(text, AcceptedFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return date;

        throw new JsonException($"'{text}' no es una fecha válida. Formato esperado: {Format}.");
    }

    public override void Write(Utf8JsonWriter writer, DateOnly value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(value.ToString(Format, CultureInfo.InvariantCulture));
    }
}
