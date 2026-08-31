using NovaFE.Domain.Common;

namespace NovaFE.Domain.Ecf;

/// <summary>
/// Estado del comprobante en el ciclo de vida de NovaFE (contrato público de la
/// API). Se persiste por <see cref="PublicName"/> (el mismo token que expone la
/// API); <see cref="Enumeration{T}.Name"/> es el identificador interno.
/// <para>
/// El flujo: <see cref="Signed"/> (Módulo 12: firmado y encolado) →
/// <see cref="Submitted"/> (enviado a la DGII, hay <c>TrackId</c>) →
/// <see cref="Accepted"/> / <see cref="AcceptedConditional"/> /
/// <see cref="Rejected"/>. <see cref="Review"/> (Módulo 4: la DGII no resolvió
/// tras el ladder de polling) y <see cref="Failed"/> (agotó los reintentos de
/// transporte) se reencolan con <c>POST /ecf/{id}/retry</c>.
/// </para>
/// </summary>
public sealed record EcfStatus(int Id, string Name, string PublicName) : Enumeration<EcfStatus>(Id, Name)
{
    /// <summary>Secuencia asignada, XML armado, cuadrado y firmado, y encolado para envío.</summary>
    public static readonly EcfStatus Signed = new(1, nameof(Signed), "signed");

    /// <summary>Enviado a la DGII; hay <c>TrackId</c> y se espera el resultado.</summary>
    public static readonly EcfStatus Submitted = new(2, nameof(Submitted), "submitted");

    /// <summary>La DGII lo aceptó: tiene validez fiscal (código 1).</summary>
    public static readonly EcfStatus Accepted = new(3, nameof(Accepted), "accepted");

    /// <summary>Aceptado condicional: tiene validez fiscal pese a una observación (código 4).</summary>
    public static readonly EcfStatus AcceptedConditional = new(4, nameof(AcceptedConditional), "accepted_conditional");

    /// <summary>La DGII lo rechazó: nulidad del comprobante (código 2).</summary>
    public static readonly EcfStatus Rejected = new(5, nameof(Rejected), "rejected");

    /// <summary>La DGII no dio un resultado definitivo tras el ladder de polling — revisión manual.</summary>
    public static readonly EcfStatus Review = new(6, nameof(Review), "review");

    /// <summary>No se pudo enviar tras agotar los reintentos de transporte.</summary>
    public static readonly EcfStatus Failed = new(7, nameof(Failed), "failed");

    /// <summary>Estado final del ciclo con la DGII (no se reintenta ni avanza).</summary>
    public bool IsTerminal => this == Accepted || this == AcceptedConditional || this == Rejected;

    /// <summary>Se puede reencolar para un nuevo intento de envío.</summary>
    public bool IsRetriable => this == Failed || this == Review;

    /// <summary>El comprobante ya se envió a la DGII (hay o hubo un <c>TrackId</c>).</summary>
    public bool WasSubmitted => this == Submitted || IsTerminal || this == Review;

    /// <summary>Resuelve el estado a partir del código de la DGII (1/2/4).</summary>
    public static EcfStatus FromDgiiCode(int code) => code switch
    {
        1 => Accepted,
        2 => Rejected,
        4 => AcceptedConditional,
        _ => throw new ArgumentOutOfRangeException(
            nameof(code), code, "Solo los códigos 1, 2 y 4 de la DGII son estados definitivos."),
    };
}
