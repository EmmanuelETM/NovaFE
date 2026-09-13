namespace NovaFE.Application.Ecf.Contracts;

/// <summary>
/// El <c>data.object</c> de <c>ecf.duplicate_suspected</c> (modo <c>observar</c> de
/// la detección de duplicados) — a diferencia del resto de los eventos del ciclo de
/// vida del e-CF, lleva también una referencia al comprobante con el que coincidió
/// la huella, para que el consumidor pueda comparar los dos.
/// </summary>
public sealed record EcfDuplicateSuspectedPayload(EcfDto Ecf, Guid PreviousEcfId, string PreviousEncf);
