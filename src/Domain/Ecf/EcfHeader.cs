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
/// <param name="Currency"><c>&lt;TipoMoneda&gt;</c> — ISO Tabla II. Null = DOP.</param>
/// <param name="ExchangeRate"><c>&lt;TipoCambio&gt;</c> — obligatorio si hay moneda.</param>
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
    string? Currency = null,
    decimal? ExchangeRate = null);
