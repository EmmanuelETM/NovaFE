using NovaFE.Domain.Common;

namespace NovaFE.Domain.Ecf;

/// <summary><c>&lt;TipoPago&gt;</c>: 1 = Contado, 2 = Crédito, 3 = Gratuito.</summary>
public sealed record PaymentCondition(int Id, string Name) : Enumeration<PaymentCondition>(Id, Name)
{
    public static readonly PaymentCondition Cash = new(1, nameof(Cash));
    public static readonly PaymentCondition Credit = new(2, nameof(Credit));
    public static readonly PaymentCondition Free = new(3, nameof(Free));
}

/// <summary>
/// <c>&lt;FormaPago&gt;</c> de <c>&lt;TablaFormasPago&gt;</c>: 1 = Efectivo,
/// 2 = Cheque/Transferencia/Depósito, 3 = Tarjeta, 4 = Venta a Crédito,
/// 5 = Bonos/Certificados, 6 = Permuta, 7 = Nota de crédito, 8 = Otras.
/// </summary>
public sealed record PaymentMethodType(int Id, string Name) : Enumeration<PaymentMethodType>(Id, Name)
{
    public static readonly PaymentMethodType Cash = new(1, nameof(Cash));
    public static readonly PaymentMethodType CheckTransfer = new(2, nameof(CheckTransfer));
    public static readonly PaymentMethodType Card = new(3, nameof(Card));
    public static readonly PaymentMethodType Credit = new(4, nameof(Credit));
    public static readonly PaymentMethodType Voucher = new(5, nameof(Voucher));
    public static readonly PaymentMethodType Swap = new(6, nameof(Swap));
    public static readonly PaymentMethodType CreditNote = new(7, nameof(CreditNote));
    public static readonly PaymentMethodType Other = new(8, nameof(Other));
}

/// <summary>Una forma de pago de <c>&lt;TablaFormasPago&gt;</c> (hasta 7).</summary>
/// <param name="Method">Código de forma de pago.</param>
/// <param name="Amount"><c>&lt;MontoPago&gt;</c> — ≥ 0.</param>
public sealed record EcfPaymentMethod(PaymentMethodType Method, decimal Amount);

/// <summary>Bloque de pago del encabezado.</summary>
/// <param name="Condition"><c>&lt;TipoPago&gt;</c>.</param>
/// <param name="DueDate"><c>&lt;FechaLimitePago&gt;</c> — obligatoria si <see cref="Condition"/> es a crédito.</param>
/// <param name="Methods"><c>&lt;TablaFormasPago&gt;</c> — 0 a 7 entradas.</param>
public sealed record EcfPayment(
    PaymentCondition Condition,
    DateOnly? DueDate,
    IReadOnlyList<EcfPaymentMethod> Methods);
