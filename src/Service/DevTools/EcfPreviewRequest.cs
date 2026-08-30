namespace NovaFE.Service.DevTools;

/// <summary>
/// Cuerpo del <c>POST /api/v1/dev/ecf-preview</c> — una forma "cruda" del e-CF para
/// generar XML y verlo, sin pasar por el payload curado de la API real. Todo tiene
/// defaults razonables; en el mínimo alcanza con <c>{ "type": 31, "lines": [...] }</c>.
/// Solo existe en Development.
/// </summary>
public sealed record EcfPreviewRequest
{
    public int Type { get; init; } = 31;
    public string? Encf { get; init; }
    public DateOnly? IssueDate { get; init; }
    public string IncomeType { get; init; } = "01";
    public bool PricesIncludeTax { get; init; }

    /// <summary>Null = 31-dic del año siguiente; los tipos 32/34 no la llevan.</summary>
    public DateOnly? SequenceExpiresOn { get; init; }
    public decimal NonInvoiceableAmount { get; init; }

    public PreviewParty Issuer { get; init; } = PreviewParty.DefaultIssuer;
    public PreviewBuyer Buyer { get; init; } = PreviewBuyer.Default;
    public PreviewPayment Payment { get; init; } = new();
    public IReadOnlyList<PreviewLine> Lines { get; init; } = [new()];

    public PreviewReference? Reference { get; init; }
    public PreviewForeignCurrency? ForeignCurrency { get; init; }
    public IReadOnlyList<PreviewAdjustment>? GlobalAdjustments { get; init; }
}

public sealed record PreviewParty(string Rnc, string Name, string? Address = null, string? Email = null)
{
    public static readonly PreviewParty DefaultIssuer =
        new("132786262", "AlMax Solutions EIRL", "Carretera Mella 1099, Santo Domingo", "facturacion@almax.do");
}

public sealed record PreviewBuyer(
    string Name = "Activatec SRL",
    string? Rnc = "132056892",
    string? ForeignId = null,
    string? Email = null)
{
    public static readonly PreviewBuyer Default = new();
}

public sealed record PreviewPayment(
    int Condition = 2,
    DateOnly? DueDate = null,
    IReadOnlyList<PreviewPaymentMethod>? Methods = null);

public sealed record PreviewPaymentMethod(int Method, decimal Amount);

public sealed record PreviewLine(
    int? Number = null,
    int Rate = 1,
    string Name = "Servicio de consultoría",
    int Kind = 2,
    decimal Quantity = 1m,
    decimal UnitPrice = 2000m,
    string? Description = null,
    string? UnitOfMeasure = "43",
    decimal Discount = 0m,
    decimal Surcharge = 0m,
    decimal AdditionalTaxes = 0m,
    PreviewRetention? Retention = null,
    IReadOnlyList<PreviewAdditionalTax>? AdditionalTaxDetail = null);

public sealed record PreviewRetention(int Agent = 1, decimal ItbisWithheld = 0m, decimal IsrWithheld = 0m);

public sealed record PreviewAdditionalTax(
    string Code,
    decimal Rate,
    decimal IscEspecifico = 0m,
    decimal IscAdvalorem = 0m,
    decimal Otros = 0m);

public sealed record PreviewReference(
    string ModifiedNcf,
    DateOnly ModifiedNcfDate,
    int Code = 3,
    string? OtherIssuerRnc = null);

public sealed record PreviewForeignCurrency(
    string Currency,
    decimal ExchangeRate,
    PreviewForeignCurrencyTotals Totals);

public sealed record PreviewForeignCurrencyTotals(
    decimal? MontoGravadoTotal = null,
    decimal? MontoGravadoI1 = null,
    decimal? MontoGravadoI2 = null,
    decimal? MontoGravadoI3 = null,
    decimal? MontoExento = null,
    decimal? TotalItbis = null,
    decimal? TotalItbis1 = null,
    decimal? TotalItbis2 = null,
    decimal? TotalItbis3 = null,
    decimal? MontoTotal = null);

public sealed record PreviewAdjustment(
    int Line,
    int Kind = 1,
    int AffectsRate = 1,
    decimal Amount = 0m,
    bool Norma1007 = false,
    string? Description = null,
    decimal? Percentage = null);

/// <summary>Cuerpo del <c>POST /api/v1/dev/ecf-preview/rfce</c>.</summary>
public sealed record RfcePreviewRequest
{
    public EcfPreviewRequest Document { get; init; } = new() { Type = 32, SequenceExpiresOn = null };

    /// <summary>Los 6 caracteres del código de seguridad (Módulo 3). Default: uno de ejemplo.</summary>
    public string SecurityCode { get; init; } = "aB3xZ9";
}
