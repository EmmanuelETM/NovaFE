using System.Text.Json.Serialization;
using NovaFE.Domain.Dgii;

namespace NovaFE.Application.Ecf.Contracts;

/// <summary>
/// Vista de un comprobante emitido (respuesta de <c>POST /ecf</c> y <c>GET /ecf/{id}</c>).
/// El detalle comercial (comprador, líneas, montos) vive en el XML firmado, que se
/// sirve por <c>GET /ecf/{id}/xml</c>; aquí solo va la identidad fiscal del
/// comprobante, su estado y el resultado del intercambio con la DGII (<see cref="Dgii"/>).
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
    // --- Módulo 4: intercambio con la DGII ---
    // Estos entran planos (Dapper / el ensamblador) pero salen agrupados en `dgii`.
    [property: JsonIgnore] string? TrackId = null,
    [property: JsonIgnore] DateTimeOffset? SubmittedAt = null,
    [property: JsonIgnore] DateTimeOffset? DgiiProcessedAt = null,
    [property: JsonIgnore] DateTimeOffset? DgiiReceivedAt = null,
    [property: JsonIgnore] int? DgiiStatusCode = null,
    [property: JsonIgnore] string? DgiiStatusText = null,
    [property: JsonIgnore] bool? SequenceUsed = null,
    [property: JsonIgnore] IReadOnlyList<DgiiMessage>? DgiiMessages = null)
{
    /// <summary>
    /// Lo que pasó con la DGII: <c>trackId</c>, estado (texto y código), mensajes,
    /// <c>secuenciaUtilizada</c> y los instantes de envío, recepción y resolución.
    /// <c>null</c> mientras el comprobante no se haya enviado.
    /// </summary>
    public EcfDgiiExchange? Dgii => TrackId is null && DgiiStatusCode is null && SubmittedAt is null
        ? null
        : new EcfDgiiExchange(
            TrackId: TrackId,
            Status: DgiiStatusText,
            StatusCode: DgiiStatusCode,
            SequenceUsed: SequenceUsed,
            Messages: DgiiMessages ?? [],
            SubmittedAt: SubmittedAt,
            ReceivedAt: DgiiReceivedAt,
            ProcessedAt: DgiiProcessedAt);

    /// <summary>Enlaces a los recursos relacionados del comprobante.</summary>
    public EcfLinks Links => new(
        Self: $"/api/v1/ecf/{Id}",
        Xml: $"/api/v1/ecf/{Id}/xml",
        RfceXml: SubmitsRfce ? $"/api/v1/ecf/{Id}/xml?rfce=true" : null,
        Representation: $"/api/v1/ecf/{Id}/representation");
}

/// <summary>
/// El intercambio con la DGII de un comprobante: lo que ella respondió más los
/// instantes en que NovaFE registró cada paso.
/// </summary>
/// <param name="TrackId">Identificador que la DGII asigna al recibir el comprobante.</param>
/// <param name="Status">El <c>estado</c> textual de la DGII ("Aceptado", "Rechazado", "En Proceso"…); en el idioma en que lo manda ella.</param>
/// <param name="StatusCode">Código de estado de la DGII: 1 aceptado · 2 rechazado · 3 en proceso · 4 aceptado condicional.</param>
/// <param name="SequenceUsed">
/// <c>secuenciaUtilizada</c> de la DGII: <c>false</c> = el e-NCF no se consumió
/// (firma/XML inválidos); <c>true</c> o ausente = consumido.
/// </param>
/// <param name="Messages">Observaciones o motivo de rechazo que devolvió la DGII.</param>
/// <param name="SubmittedAt">Instante en que la DGII confirmó la recepción (hay <see cref="TrackId"/>).</param>
/// <param name="ReceivedAt">La <c>fechaRecepcion</c> que informó la DGII; ausente si no la dio (p. ej. RFCE).</param>
/// <param name="ProcessedAt">Instante en que la DGII dio un resultado definitivo.</param>
public sealed record EcfDgiiExchange(
    string? TrackId,
    string? Status,
    int? StatusCode,
    bool? SequenceUsed,
    IReadOnlyList<DgiiMessage> Messages,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? ReceivedAt,
    DateTimeOffset? ProcessedAt);

/// <summary>Enlaces (relativos) a los recursos del comprobante.</summary>
/// <param name="Self">El comprobante y su estado.</param>
/// <param name="Xml">El XML firmado (<c>&lt;ECF&gt;</c>).</param>
/// <param name="RfceXml">El resumen firmado (<c>&lt;RFCE&gt;</c>); solo cuando <c>submitsRfce</c>.</param>
/// <param name="Representation">La Representación Impresa en PDF.</param>
public sealed record EcfLinks(string Self, string Xml, string? RfceXml, string Representation);

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
