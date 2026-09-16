namespace NovaFE.Application.Ecf.Contracts;

/// <summary>
/// Resultado de <c>POST /api/v1/ecf/validate</c>: el payload pasó la matriz de
/// validación por tipo (Módulo 2) y el cálculo de totales (Módulo 6) sin asignar
/// una secuencia real, firmar ni persistir nada. Un payload que no pasa nunca
/// llega a este DTO — sale como <c>400</c>, igual que <c>POST /ecf</c>.
/// </summary>
/// <param name="SampleEncf">
/// Placeholder (<c>E{tipo}0000000000</c>) — nunca un e-NCF real, no se asignó
/// ninguna secuencia para construir este documento.
/// </param>
/// <param name="SequenceExpiresOnEstimate">
/// Estimado (31-dic del año siguiente a <see cref="IssueDate"/>) cuando el tipo
/// lo lleva; no viene de una secuencia real. <c>null</c> en los tipos que no lo
/// llevan (32, 34).
/// </param>
/// <param name="ExpectConditionalAcceptance">
/// Los totales quedarían fuera de la tolerancia de cuadratura (RF-06.6) — la
/// DGII probablemente aceptaría el comprobante de forma condicional si se emite así.
/// </param>
public sealed record ValidateEcfResultDto(
    int Type,
    string TypeName,
    string SampleEncf,
    DateOnly IssueDate,
    DateOnly? SequenceExpiresOnEstimate,
    decimal MontoGravadoTotal,
    decimal MontoExento,
    decimal TotalItbis,
    decimal MontoImpuestoAdicional,
    decimal MontoTotal,
    bool ExpectConditionalAcceptance);
