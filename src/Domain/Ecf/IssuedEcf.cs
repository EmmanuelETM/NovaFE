using ErrorOr;
using NovaFE.Domain.Common;
using NovaFE.Domain.Common.Entities;
using NovaFE.Domain.Dgii;
using NovaFE.Domain.Sequences;

namespace NovaFE.Domain.Ecf;

/// <summary>
/// Un comprobante fiscal electrónico <b>emitido</b> — firmado y persistido. Es el
/// agregado que vive en la tabla <c>issued_ecf</c> y que la API expone; no
/// confundir con <see cref="EcfDocument"/>, el modelo fiscal transitorio del que
/// se construye.
/// <para>
/// Nace en <see cref="EcfStatus.Signed"/> (Módulo 12). El envío a la DGII y el
/// polling del <c>TrackId</c> (Módulo 4) lo llevan por
/// <see cref="EcfStatus.Submitted"/> hasta <see cref="EcfStatus.Accepted"/> /
/// <see cref="EcfStatus.AcceptedConditional"/> / <see cref="EcfStatus.Rejected"/>,
/// vía los métodos <c>Mark*</c> (defensa en profundidad: validan el estado de origen).
/// </para>
/// </summary>
public sealed class IssuedEcf : Entity<Guid>, ITenantOwned, IAuditableEntity, ISoftDeletable
{
    // Required by EF Core.
    private IssuedEcf()
    {
    }

    private IssuedEcf(
        Guid id,
        EcfType type,
        DgiiEnvironment environment,
        Encf encf,
        DateOnly? sequenceExpiresOn,
        DateOnly issueDate,
        string? internalInvoiceNumber,
        string? buyerRnc,
        string? buyerName,
        EcfStatus status,
        EcfTotalsSnapshot totals,
        decimal montoTotal,
        bool expectedConditionalAcceptance,
        DateTimeOffset signedAt,
        string signatureValue,
        string securityCode,
        string documentHash,
        string qrUrl,
        bool submitsRfce,
        string ecfXml,
        string? rfceXml)
        : base(id)
    {
        Type = type;
        Environment = environment;
        Encf = encf;
        SequenceExpiresOn = sequenceExpiresOn;
        IssueDate = issueDate;
        InternalInvoiceNumber = internalInvoiceNumber;
        BuyerRnc = buyerRnc;
        BuyerName = buyerName;
        Status = status;
        Totals = totals;
        MontoTotal = montoTotal;
        ExpectedConditionalAcceptance = expectedConditionalAcceptance;
        SignedAt = signedAt;
        SignatureValue = signatureValue;
        SecurityCode = securityCode;
        DocumentHash = documentHash;
        QrUrl = qrUrl;
        SubmitsRfce = submitsRfce;
        EcfXml = ecfXml;
        RfceXml = rfceXml;
    }

    public Guid TenantId { get; private set; }

    public EcfType Type { get; private set; } = null!;

    /// <summary>Ambiente de la DGII en el que se emitió (determina el certificado y el pool de secuencias).</summary>
    public DgiiEnvironment Environment { get; private set; } = null!;

    /// <summary>e-NCF asignado (Módulo 7).</summary>
    public Encf Encf { get; private set; }

    public DateOnly? SequenceExpiresOn { get; private set; }

    public DateOnly IssueDate { get; private set; }

    /// <summary><c>&lt;NumeroFacturaInterna&gt;</c> — clave de dedup de negocio.</summary>
    public string? InternalInvoiceNumber { get; private set; }

    /// <summary>RNC/cédula del comprador — snapshot desnormalizado para listado/búsqueda.</summary>
    public string? BuyerRnc { get; private set; }

    /// <summary>Razón social del comprador — snapshot desnormalizado.</summary>
    public string? BuyerName { get; private set; }

    public EcfStatus Status { get; private set; } = null!;

    /// <summary>Totales del comprobante, calculados por Módulo 6 al emitir.</summary>
    public EcfTotalsSnapshot Totals { get; private set; } = null!;

    /// <summary>
    /// <c>MontoTotal</c> — columna desnormalizada (= <c>Totals.MontoTotal</c>) para
    /// ordenar y filtrar el listado sin abrir el JSON de totales.
    /// </summary>
    public decimal MontoTotal { get; private set; }

    /// <summary>
    /// La cuadratura declarada por el cliente quedó fuera de la tolerancia (RF-06.6):
    /// la DGII probablemente devolverá "aceptado condicional". Nunca bloquea la emisión.
    /// </summary>
    public bool ExpectedConditionalAcceptance { get; private set; }

    public DateTimeOffset SignedAt { get; private set; }

    /// <summary>Base64 de <c>&lt;SignatureValue&gt;</c>.</summary>
    public string SignatureValue { get; private set; } = null!;

    /// <summary>6 primeros caracteres del <see cref="SignatureValue"/> (código del QR / RI).</summary>
    public string SecurityCode { get; private set; } = null!;

    /// <summary>SHA-256 en hex del XML firmado (RF-03.4).</summary>
    public string DocumentHash { get; private set; } = null!;

    /// <summary>URL del timbre QR de la Representación Impresa.</summary>
    public string QrUrl { get; private set; } = null!;

    /// <summary>El envío a la DGII usa el RFCE (tipo 32 &lt; DOP 250 000).</summary>
    public bool SubmitsRfce { get; private set; }

    /// <summary>El <c>&lt;ECF&gt;</c> firmado. Se guarda siempre.</summary>
    public string EcfXml { get; private set; } = null!;

    /// <summary>El <c>&lt;RFCE&gt;</c> firmado — solo cuando <see cref="SubmitsRfce"/>.</summary>
    public string? RfceXml { get; private set; }

    // --- Módulo 4: envío a la DGII y seguimiento --------------------------

    /// <summary><c>TrackId</c> devuelto por la DGII al recibir el comprobante. Null hasta el envío.</summary>
    public string? TrackId { get; private set; }

    /// <summary>Instante en que la DGII confirmó la recepción (hay <see cref="TrackId"/>).</summary>
    public DateTimeOffset? SubmittedAt { get; private set; }

    /// <summary>Instante en que la DGII dio un resultado definitivo (aceptado/rechazado).</summary>
    public DateTimeOffset? DgiiProcessedAt { get; private set; }

    /// <summary>Código de estado de la DGII (1 aceptado / 2 rechazado / 4 aceptado condicional).</summary>
    public int? DgiiStatusCode { get; private set; }

    /// <summary>El <c>estado</c> textual que devolvió la DGII ("Aceptado", "Rechazado"…). Null hasta la resolución.</summary>
    public string? DgiiStatusText { get; private set; }

    /// <summary><c>fechaRecepcion</c> informada por la DGII. Null si no la dio (p. ej. RFCE síncrono).</summary>
    public DateTimeOffset? DgiiReceivedAt { get; private set; }

    /// <summary>Mensajes de la DGII (observaciones, motivo de rechazo). Vacío hasta que haya alguno.</summary>
    public IReadOnlyList<DgiiMessage> DgiiMessages { get; private set; } = [];

    /// <summary>
    /// <c>secuenciaUtilizada</c> de la DGII: <c>false</c> = el e-NCF se puede
    /// reutilizar (firma/XML inválidos, etc.); <c>true</c> o null = quemado.
    /// </summary>
    public bool? SequenceUsable { get; private set; }

    /// <summary>Cantidad de intentos de envío (no de polling).</summary>
    public int SubmissionAttempts { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }

    /// <summary>
    /// Construye el comprobante emitido a partir del modelo fiscal cuadrado
    /// (<paramref name="document"/>) y la salida de la firma (<paramref name="signed"/>).
    /// </summary>
    /// <param name="expectConditionalAcceptance">
    /// Los montos declarados por el cliente (por línea o en el encabezado) quedaron
    /// fuera de la tolerancia; la DGII probablemente los acepte de forma condicional.
    /// </param>
    public static IssuedEcf FromSigned(
        EcfDocument document,
        SignedEcf signed,
        DgiiEnvironment environment,
        bool expectConditionalAcceptance = false)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(signed);
        ArgumentNullException.ThrowIfNull(environment);

        var header = document.Header;

        return new IssuedEcf(
            Guid.CreateVersion7(),
            document.Type,
            environment,
            header.Encf,
            header.SequenceExpiresOn,
            header.IssueDate,
            header.Issuer.InternalInvoiceNumber,
            header.Buyer.Rnc?.Value,
            header.Buyer.Name,
            EcfStatus.Signed,
            EcfTotalsSnapshot.From(document.Totals),
            document.Totals.MontoTotal,
            expectConditionalAcceptance || document.Calculation.Tolerance.ExpectConditionalAcceptance,
            signed.SignedAt,
            signed.SignatureValue,
            signed.SecurityCode,
            signed.DocumentHash,
            signed.QrUrl,
            signed.SubmitsRfce,
            signed.EcfXml,
            signed.RfceXml);
    }

    // --- Transiciones de Módulo 4 ----------------------------------------

    /// <summary>La DGII recibió el comprobante y devolvió un <paramref name="trackId"/>.</summary>
    public ErrorOr<Success> MarkSubmitted(string trackId, DateTimeOffset at)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(trackId);

        if (Status != EcfStatus.Signed)
            return IssuedEcfErrors.InvalidTransition(Status.PublicName, EcfStatus.Submitted.PublicName);

        TrackId = trackId;
        SubmittedAt = at;
        SubmissionAttempts++;
        Status = EcfStatus.Submitted;
        return Result.Success;
    }

    /// <summary>
    /// La DGII aceptó el comprobante (código 1) o lo aceptó de forma condicional
    /// (código 4). El RFCE puede resolver sin pasar por <see cref="EcfStatus.Submitted"/>.
    /// </summary>
    public ErrorOr<Success> MarkAccepted(DateTimeOffset at, DgiiVerdict verdict)
    {
        ArgumentNullException.ThrowIfNull(verdict);

        if (Status != EcfStatus.Signed && Status != EcfStatus.Submitted)
            return IssuedEcfErrors.InvalidTransition(Status.PublicName, "accepted");

        ApplyVerdict(at, verdict);
        Status = verdict.StatusCode == 4 ? EcfStatus.AcceptedConditional : EcfStatus.Accepted;
        return Result.Success;
    }

    /// <summary>La DGII rechazó el comprobante (código 2): nulidad.</summary>
    public ErrorOr<Success> MarkRejected(DateTimeOffset at, DgiiVerdict verdict)
    {
        ArgumentNullException.ThrowIfNull(verdict);

        if (Status != EcfStatus.Signed && Status != EcfStatus.Submitted)
            return IssuedEcfErrors.InvalidTransition(Status.PublicName, EcfStatus.Rejected.PublicName);

        ApplyVerdict(at, verdict);
        Status = EcfStatus.Rejected;
        return Result.Success;
    }

    private void ApplyVerdict(DateTimeOffset at, DgiiVerdict verdict)
    {
        DgiiProcessedAt = at;
        DgiiStatusCode = verdict.StatusCode;
        DgiiStatusText = verdict.StatusText;
        // La DGII manda la fecha en hora dominicana; se guarda como instante UTC.
        DgiiReceivedAt = verdict.ReceivedAt?.ToUniversalTime();
        DgiiMessages = verdict.Messages ?? [];
        SequenceUsable = verdict.SequenceUsed;
    }

    /// <summary>
    /// Enviado, con <see cref="TrackId"/>, pero la DGII no dio un resultado
    /// definitivo tras el ladder de polling. Necesita revisión manual.
    /// </summary>
    public ErrorOr<Success> MarkForReview(string reason)
    {
        if (Status != EcfStatus.Submitted)
            return IssuedEcfErrors.InvalidTransition(Status.PublicName, EcfStatus.Review.PublicName);

        DgiiMessages = [new DgiiMessage(0, reason)];
        Status = EcfStatus.Review;
        return Result.Success;
    }

    /// <summary>No se pudo enviar tras agotar los reintentos de transporte.</summary>
    public ErrorOr<Success> MarkFailed(string reason)
    {
        if (Status != EcfStatus.Signed)
            return IssuedEcfErrors.InvalidTransition(Status.PublicName, EcfStatus.Failed.PublicName);

        DgiiMessages = [new DgiiMessage(0, reason)];
        Status = EcfStatus.Failed;
        return Result.Success;
    }

    /// <summary>Reencola un comprobante <c>failed</c>/<c>review</c> para un nuevo intento de envío.</summary>
    public ErrorOr<Success> RequeueForRetry()
    {
        if (!Status.IsRetriable)
            return IssuedEcfErrors.NotRetriable(Status.PublicName);

        Status = EcfStatus.Signed;
        return Result.Success;
    }
}
