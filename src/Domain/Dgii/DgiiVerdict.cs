namespace NovaFE.Domain.Dgii;

/// <summary>
/// El resultado <b>definitivo</b> que la DGII devolvió para un comprobante
/// (aceptado / aceptado condicional / rechazado). Empaqueta todo lo que la
/// respuesta trae para no arrastrar media docena de parámetros por las
/// transiciones del agregado.
/// </summary>
/// <param name="StatusCode">Código de estado: 1 aceptado · 2 rechazado · 4 aceptado condicional.</param>
/// <param name="StatusText">El <c>estado</c> textual de la DGII ("Aceptado", "Rechazado"…); null si no lo trae.</param>
/// <param name="Messages">Observaciones o motivo de rechazo.</param>
/// <param name="SequenceUsed">
/// <c>secuenciaUtilizada</c>: <c>false</c> = el e-NCF no se consumió (firma/XML
/// inválidos); <c>true</c> o null = consumido.
/// </param>
/// <param name="ReceivedAt"><c>fechaRecepcion</c> informada por la DGII; null si no la dio (p. ej. RFCE síncrono).</param>
public sealed record DgiiVerdict(
    int StatusCode,
    string? StatusText,
    IReadOnlyList<DgiiMessage> Messages,
    bool? SequenceUsed,
    DateTimeOffset? ReceivedAt);
