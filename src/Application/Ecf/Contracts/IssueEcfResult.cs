namespace NovaFE.Application.Ecf.Contracts;

/// <summary>
/// Salida de <c>IssueEcfUseCase</c>. <see cref="WasCreated"/> distingue una emisión
/// nueva (<c>201 Created</c>) de la respuesta repetida de una idempotencia o un
/// <c>NumeroFacturaInterna</c> ya usado (<c>200 OK</c>).
/// </summary>
public sealed record IssueEcfResult(EcfDto Ecf, bool WasCreated);
