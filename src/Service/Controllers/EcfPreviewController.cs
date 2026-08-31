using Asp.Versioning;
using ErrorOr;
using NovaFE.Application.Ecf.Interfaces;
using NovaFE.Domain.Common;
using NovaFE.Domain.Ecf;
using NovaFE.Service.Common;
using NovaFE.Service.DevTools;
using Microsoft.AspNetCore.Mvc;

namespace NovaFE.Service.Controllers;

/// <summary>
/// <b>Solo Development.</b> Genera el XML del e-CF (o del RFCE) para verlo y
/// diffear. Cada respuesta trae el resultado de validar contra el XSD oficial.
/// <para>
/// Por defecto devuelve el XML <b>sin firmar</b> (como sale de Módulo 2). Con
/// <c>?signed=true</c> lo pasa por Módulo 3 y lo firma con un certificado
/// autofirmado efímero — sirve para ver la forma del documento firmado; esa firma
/// <b>no</b> la aceptaría la DGII.
/// </para>
/// <para>
/// Fuera de Development este controller no existe (ver
/// <see cref="RemoveDevelopmentOnlyConvention"/>). Ejemplos de uso en
/// <c>src/Service/NovaFE.Service.http</c>.
/// </para>
/// </summary>
[DevelopmentOnly]
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/dev/[controller]")]
public sealed class EcfPreviewController(
    IEcfXmlSerializer serializer,
    IRfceSerializer rfceSerializer,
    IEcfXsdValidator validator,
    DevEcfSigner devSigner) : ApiController
{
    private const string SignedNote =
        "Firmado con un certificado autofirmado efímero — la DGII no aceptaría esta firma.";

    /// <summary>Lista los ejemplos disponibles (uno por tipo de comprobante).</summary>
    [HttpGet("samples")]
    public IActionResult Samples()
        => Ok(EcfSampleCatalog.All.Select(sample => new { sample.Slug, sample.Title }));

    /// <summary>
    /// El XML de un ejemplo. <c>?rfce=true</c> devuelve el RFCE (solo si el ejemplo
    /// es tipo 32); <c>?signed=true</c> lo firma; <c>?raw=true</c> devuelve el XML
    /// crudo en vez del JSON.
    /// </summary>
    [HttpGet("samples/{slug}")]
    public IActionResult Sample(
        string slug,
        [FromQuery] bool rfce = false,
        [FromQuery(Name = "signed")] bool withSignature = false,
        [FromQuery] bool raw = false)
    {
        if (EcfSampleCatalog.Find(slug) is not { } sample)
            return Problem([Error.NotFound("EcfPreview.SampleNotFound", $"No hay ejemplo '{slug}'.")]);

        if (withSignature)
            return Signed(sample.Document, wantRfce: rfce, raw);

        return rfce
            ? Rfce(sample.Document, "aB3xZ9", raw)
            : Ecf(sample.Document, raw);
    }

    /// <summary>
    /// Genera el XML <c>&lt;ECF&gt;</c> a partir de un cuerpo crudo. En el mínimo:
    /// <c>{ "type": 31, "lines": [{ "unitPrice": 1000 }] }</c>. <c>?signed=true</c>
    /// lo firma; <c>?raw=true</c> devuelve el XML crudo.
    /// </summary>
    [HttpPost]
    public IActionResult Preview(
        [FromBody] EcfPreviewRequest request,
        [FromQuery(Name = "signed")] bool withSignature = false,
        [FromQuery] bool raw = false)
        => EcfPreviewMapper.ToDocument(request).Match(
            doc => withSignature ? Signed(doc, wantRfce: false, raw) : Ecf(doc, raw),
            Problem);

    /// <summary>Genera el XML <c>&lt;RFCE&gt;</c> (el <c>document</c> debe ser tipo 32).</summary>
    [HttpPost("rfce")]
    public IActionResult PreviewRfce(
        [FromBody] RfcePreviewRequest request,
        [FromQuery(Name = "signed")] bool withSignature = false,
        [FromQuery] bool raw = false)
        => EcfPreviewMapper.ToDocument(request.Document).Match(
            doc => withSignature
                ? Signed(doc, wantRfce: true, raw)
                : Rfce(doc, request.SecurityCode, raw),
            Problem);

    // ---- helpers --------------------------------------------------------

    private IActionResult Ecf(EcfDocument document, bool raw)
    {
        var xml = serializer.Serialize(document, EcfSampleCatalog.SignedAt);
        var xsd = validator.Validate(WithStubSignature(xml, "ECF"), document.Type);
        return Render(xml, xsd, raw);
    }

    private IActionResult Rfce(EcfDocument document, string securityCode, bool raw)
    {
        if (document.Type != EcfType.Consumo)
            return Problem([Error.Validation("EcfPreview.RfceOnlyType32", "El RFCE solo aplica al tipo 32.")]);

        var xml = rfceSerializer.Serialize(document, securityCode);
        var xsd = validator.ValidateRfce(WithStubSignature(xml, "RFCE"));
        return Render(xml, xsd, raw);
    }

    private IActionResult Signed(EcfDocument document, bool wantRfce, bool raw)
    {
        var result = devSigner.Sign(document, forceRfce: wantRfce);

        if (wantRfce)
        {
            if (result.RfceXml is null)
                return Problem([Error.Validation("EcfPreview.RfceOnlyType32", "El RFCE solo aplica al tipo 32.")]);

            return raw
                ? RawXml(result.RfceXml, result.RfceXsdValid ?? false)
                : Ok(new
                {
                    xml = result.RfceXml,
                    xsdValid = result.RfceXsdValid,
                    xsdError = result.RfceXsdError,
                    result.SecurityCode,
                    result.DocumentHash,
                    result.QrUrl,
                    note = SignedNote,
                });
        }

        return raw
            ? RawXml(result.EcfXml, result.EcfXsdValid)
            : Ok(new
            {
                xml = result.EcfXml,
                xsdValid = result.EcfXsdValid,
                xsdError = result.EcfXsdError,
                rfceXml = result.RfceXml,
                result.SecurityCode,
                result.DocumentHash,
                result.QrUrl,
                note = SignedNote,
            });
    }

    private IActionResult Render(string xml, ErrorOr<Success> xsd, bool raw)
        => raw
            ? RawXml(xml, !xsd.IsError)
            : Ok(new
            {
                xml,
                xsdValid = !xsd.IsError,
                xsdError = xsd.IsError ? xsd.FirstError.Description : null,
            });

    private ContentResult RawXml(string xml, bool xsdValid)
    {
        Response.Headers["X-Ecf-Xsd-Valid"] = xsdValid ? "true" : "false";
        return Content(xml, "application/xml; charset=utf-8");
    }

    /// <summary>
    /// El e-CF pre-firma no valida solo: el XSD exige la <c>&lt;Signature&gt;</c>
    /// (<c>&lt;xs:any minOccurs="1"&gt;</c>). Se le agrega una de relleno solo para
    /// el chequeo — el XSD no valida su contenido (<c>processContents="skip"</c>).
    /// </summary>
    private static string WithStubSignature(string xml, string root) =>
        xml.Replace(
            $"</{root}>",
            $"<Signature xmlns=\"http://www.w3.org/2000/09/xmldsig#\"/></{root}>",
            StringComparison.Ordinal);
}
