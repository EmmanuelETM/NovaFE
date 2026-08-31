namespace NovaFE.Domain.Dgii;

/// <summary>
/// Un mensaje de la DGII en la respuesta de recepción o de consulta de estado
/// (<c>{ "codigo": 0, "valor": "string" }</c>). Explica una observación, un motivo
/// de rechazo o una condición. Se guarda tal cual junto al comprobante.
/// </summary>
/// <param name="Code">Código del mensaje (<c>codigo</c>); 0 cuando la DGII no lo trae.</param>
/// <param name="Value">Texto del mensaje (<c>valor</c>).</param>
public sealed record DgiiMessage(int Code, string Value);
