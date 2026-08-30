using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Xml;
using System.Xml.Schema;
using ErrorOr;
using NovaFE.Application.Ecf.Interfaces;
using NovaFE.Domain.Common;

namespace NovaFE.Infrastructure.Ecf;

/// <summary>
/// Valida el XML de e-CF contra el XSD oficial de la DGII del tipo. Los XSD van
/// embebidos en <c>Ecf/Xsd/</c>. Compilar el <see cref="XmlSchemaSet"/> es lo
/// caro (decenas de ms) y se hace una sola vez por tipo (<see cref="Lazy{T}"/>
/// para que aunque dos hilos lleguen juntos en el primer uso se compile una vez).
/// Un <see cref="XmlSchemaSet"/> ya compilado es seguro de compartir entre hilos
/// para validar; la validación en caliente de un e-CF normal ronda los 0,3–0,7 ms.
/// </summary>
internal sealed class EcfXsdValidator : IEcfXsdValidator
{
    private static readonly Assembly OwnAssembly = typeof(EcfXsdValidator).Assembly;
    private static readonly ConcurrentDictionary<int, Lazy<XmlSchemaSet?>> SchemaCache = new();

    public ErrorOr<Success> Validate(string xml, EcfType type)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        ArgumentNullException.ThrowIfNull(type);

        var schema = SchemaCache
            .GetOrAdd(type.Id, id => new Lazy<XmlSchemaSet?>(() => LoadSchema(id)))
            .Value;
        if (schema is null)
            return Error.Unexpected(
                code: "Ecf.XsdMissing",
                description: $"No hay XSD embebido para el tipo {type.Id}.");

        var violations = new List<string>();
        var settings = new XmlReaderSettings
        {
            ValidationType = ValidationType.Schema,
            Schemas = schema,
            XmlResolver = null,
        };
        settings.ValidationEventHandler += (_, e) =>
            violations.Add($"[{e.Severity}] línea {e.Exception?.LineNumber}: {e.Message}");

        try
        {
            using var reader = XmlReader.Create(new System.IO.StringReader(xml), settings);
            while (reader.Read())
            {
                // El recorrido dispara las validaciones.
            }
        }
        catch (XmlException ex)
        {
            return Error.Validation(
                code: "Ecf.MalformedXml",
                description: $"El XML no está bien formado: {ex.Message}");
        }

        if (violations.Count == 0)
            return Result.Success;

        return Error.Validation(
            code: "Ecf.XsdInvalid",
            description: "El e-CF no cumple el XSD de la DGII: " + string.Join(" | ", violations));
    }

    private static XmlSchemaSet? LoadSchema(int typeId)
    {
        var marker = string.Create(CultureInfo.InvariantCulture, $"e-CF-{typeId}-");
        var resource = Array.Find(
            OwnAssembly.GetManifestResourceNames(),
            name => name.Contains(".Ecf.Xsd.", StringComparison.Ordinal)
                    && name.Contains(marker, StringComparison.Ordinal)
                    && name.EndsWith(".xsd", StringComparison.Ordinal));

        if (resource is null)
            return null;

        using var stream = OwnAssembly.GetManifestResourceStream(resource)!;
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { XmlResolver = null });

        var set = new XmlSchemaSet { XmlResolver = null };
        set.Add(null, reader);
        set.Compile();
        return set;
    }
}
