using NovaFE.Domain.Dgii;

namespace NovaFE.Application.Ecf.Contracts;

/// <summary>
/// Vista de un comprobante emitido (respuesta de <c>POST /ecf</c> y <c>GET /ecf/{id}</c>).
/// El detalle comercial (comprador, líneas, montos) vive en el XML firmado, que se
/// sirve por <c>GET /ecf/{id}/xml</c>; aquí solo va la identidad fiscal del
/// comprobante y su estado frente a la DGII.
/// </summary>
public sealed record EcfDto(
    Guid Id,
    string Status,
    string Encf,
    int Type,
    string Environment,
    DateOnly? SequenceExpiresOn,
    DateOnly IssueDate,
    DateTimeOffset IssuedAt,
    DateTimeOffset SignedAt,
    string SecurityCode,
    string QrUrl,
    bool SubmitsRfce,
    string? InternalNumber,
    string? ToleranceWarning,
    // --- Módulo 4: envío a la DGII ---
    string? TrackId = null,
    DateTimeOffset? SubmittedAt = null,
    DateTimeOffset? DgiiProcessedAt = null,
    int? DgiiStatusCode = null,
    IReadOnlyList<DgiiMessage>? DgiiMessages = null)
{
    /// <summary>Enlaces a los recursos relacionados del comprobante.</summary>
    public EcfLinks Links => new(
        Self: $"/api/v1/ecf/{Id}",
        Xml: $"/api/v1/ecf/{Id}/xml",
        RfceXml: SubmitsRfce ? $"/api/v1/ecf/{Id}/xml?rfce=true" : null);
}

/// <summary>Enlaces (relativos) a los recursos del comprobante.</summary>
/// <param name="Self">El comprobante y su estado.</param>
/// <param name="Xml">El XML firmado (<c>&lt;ECF&gt;</c>).</param>
/// <param name="RfceXml">El resumen firmado (<c>&lt;RFCE&gt;</c>); solo cuando <c>submitsRfce</c>.</param>
public sealed record EcfLinks(string Self, string Xml, string? RfceXml);

/// <summary>Fila del listado de comprobantes emitidos.</summary>
public sealed record EcfSummaryDto(
    Guid Id,
    string Status,
    string Encf,
    int Type,
    DateOnly IssueDate,
    decimal MontoTotal,
    string? BuyerRnc,
    string? BuyerName,
    DateTimeOffset CreatedAt);
