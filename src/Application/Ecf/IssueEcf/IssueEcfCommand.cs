using System.ComponentModel;
using NovaFE.Application.Ecf.Contracts;

namespace NovaFE.Application.Ecf.IssueEcf;

/// <summary>
/// Payload de emisión de un e-CF (<c>POST /api/v1/ecf</c>). Un solo objeto
/// discriminado por <see cref="Type"/>; el servidor asigna la secuencia, arma el
/// bloque Emisor desde el perfil del tenant, calcula los totales, firma y persiste.
/// </summary>
public sealed record IssueEcfCommand
{
    /// <summary>Código DGII del tipo de e-CF: 31, 32, 33, 34, 41, 43, 44, 45, 46, 47.</summary>
    [DefaultValue(31)]
    public int Type { get; init; }

    /// <summary>Fecha de emisión (calendario dominicano). Default: hoy.</summary>
    public DateOnly? IssueDate { get; init; }

    /// <summary><c>&lt;TipoIngresos&gt;</c> — "01"…"06". Obligatorio en 31/32/33/34/44/45/46.</summary>
    public string? IncomeType { get; init; }

    /// <summary><c>true</c> si los precios de las líneas ya traen el ITBIS incluido.</summary>
    public bool PricesIncludeTax { get; init; }

    /// <summary><c>&lt;IndicadorEnvioDiferido&gt;</c> — solo contribuyentes autorizados.</summary>
    public bool DeferredDelivery { get; init; }

    /// <summary><c>&lt;MontoNoFacturable&gt;</c> — reembolsos, propina voluntaria. Puede ser negativo.</summary>
    public decimal NonInvoiceableAmount { get; init; }

    /// <summary>
    /// Ambiente de la DGII para esta emisión. Si se omite, el del perfil del emisor
    /// (<c>EmitterProfile.DefaultEnvironment</c>).
    /// </summary>
    public string? Environment { get; init; }

    /// <summary><c>&lt;NumeroFacturaInterna&gt;</c> — clave de dedup de negocio.</summary>
    public string? InternalNumber { get; init; }

    /// <summary><c>&lt;CodigoVendedor&gt;</c>.</summary>
    public string? SellerCode { get; init; }

    public EcfAdditionalInfoPayload? AdditionalInfo { get; init; }

    public EcfBuyerPayload? Buyer { get; init; }

    public EcfPaymentPayload Payment { get; init; } = new();

    public IReadOnlyList<EcfLinePayload> Lines { get; init; } = [];

    public EcfReferencePayload? Reference { get; init; }

    public IReadOnlyList<EcfGlobalAdjustmentPayload>? GlobalAdjustments { get; init; }

    public EcfForeignCurrencyPayload? ForeignCurrency { get; init; }

    public EcfShippingPayload? Shipping { get; init; }

    public EcfTransportPayload? Transport { get; init; }

    public IReadOnlyList<EcfSubtotalPayload>? Subtotals { get; init; }

    public IReadOnlyList<EcfPagePayload>? Pagination { get; init; }

    /// <summary>
    /// Valor del header <c>Idempotency-Key</c>. Lo llena el controller, no el cuerpo
    /// JSON. Si viene, un reintento con la misma clave devuelve la respuesta original.
    /// </summary>
    public string? IdempotencyKey { get; init; }

    /// <summary>Totales declarados por el cliente (chequeo de tolerancia; nunca bloquea).</summary>
    public EcfDeclaredTotalsPayload? DeclaredTotals { get; init; }
}
