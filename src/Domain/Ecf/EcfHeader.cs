using NovaFE.Domain.Common;
using NovaFE.Domain.Sequences;

namespace NovaFE.Domain.Ecf;

/// <summary>
/// Datos del <c>&lt;Encabezado&gt;</c> que no son totales. Los totales los calcula
/// <see cref="EcfDocument"/> con el motor fiscal.
/// </summary>
/// <param name="Encf">e-NCF asignado (Módulo 7).</param>
/// <param name="SequenceExpiresOn">
/// <c>&lt;FechaVencimientoSecuencia&gt;</c>. Null (y se omite del XML) para los
/// tipos 32 y 34.
/// </param>
/// <param name="IssueDate"><c>&lt;FechaEmision&gt;</c> — calendario dominicano.</param>
/// <param name="IncomeType"><c>&lt;TipoIngresos&gt;</c> — "01"…"06".</param>
/// <param name="PricesIncludeTax">
/// <c>&lt;IndicadorMontoGravado&gt;</c>: <c>true</c> (1) si los precios de las
/// líneas ya traen el ITBIS. Cada línea puede sobrescribirlo.
/// </param>
/// <param name="Issuer">Bloque emisor.</param>
/// <param name="Buyer">Bloque comprador.</param>
/// <param name="Payment">Bloque de pago.</param>
/// <param name="DeferredDelivery"><c>&lt;IndicadorEnvioDiferido&gt;</c> — solo autorizados.</param>
/// <param name="NonInvoiceableAmount"><c>&lt;MontoNoFacturable&gt;</c> — puede ser negativo.</param>
/// <param name="ForeignCurrency"><c>&lt;OtraMoneda&gt;</c> — facturación en divisa; null = solo DOP.</param>
/// <param name="Shipping"><c>&lt;InformacionesAdicionales&gt;</c> — datos de embarque; opcional.</param>
/// <param name="Transport"><c>&lt;Transporte&gt;</c> — datos de transporte; opcional.</param>
/// <param name="GlobalAdjustments"><c>&lt;DescuentosORecargos&gt;</c> — Sección D; null = ninguno.</param>
public sealed record EcfHeader(
    Encf Encf,
    DateOnly? SequenceExpiresOn,
    DateOnly IssueDate,
    string IncomeType,
    bool PricesIncludeTax,
    EcfIssuer Issuer,
    EcfBuyer Buyer,
    EcfPayment Payment,
    bool DeferredDelivery = false,
    decimal NonInvoiceableAmount = 0m,
    EcfForeignCurrency? ForeignCurrency = null,
    EcfShippingInfo? Shipping = null,
    EcfTransport? Transport = null,
    IReadOnlyList<EcfGlobalAdjustment>? GlobalAdjustments = null);
