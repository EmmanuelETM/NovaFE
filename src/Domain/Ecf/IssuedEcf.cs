using NovaFE.Domain.Common;
using NovaFE.Domain.Common.Entities;
using NovaFE.Domain.Sequences;

namespace NovaFE.Domain.Ecf;

/// <summary>
/// Un comprobante fiscal electrónico <b>emitido</b> — firmado y persistido. Es el
/// agregado que vive en la tabla <c>issued_ecf</c> y que la API expone; no
/// confundir con <see cref="EcfDocument"/>, el modelo fiscal transitorio del que
/// se construye.
/// <para>
/// v1 representa siempre un comprobante <b>firmado con éxito</b>. El envío a la
/// DGII, el <c>TrackId</c> y los estados posteriores llegan con Módulo 4.
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
    public static IssuedEcf FromSigned(EcfDocument document, SignedEcf signed, DgiiEnvironment environment)
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
            document.Calculation.Tolerance.ExpectConditionalAcceptance,
            signed.SignedAt,
            signed.SignatureValue,
            signed.SecurityCode,
            signed.DocumentHash,
            signed.QrUrl,
            signed.SubmitsRfce,
            signed.EcfXml,
            signed.RfceXml);
    }
}
