using NovaFE.Domain.Ecf;

namespace NovaFE.Application.Ecf.Contracts;

/// <summary>Vista completa de un comprobante emitido (respuesta de <c>POST /ecf</c> y <c>GET /ecf/{id}</c>).</summary>
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
    string? BuyerRnc,
    string? BuyerName,
    EcfTotalsSnapshot Totals,
    string? ToleranceWarning);

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
