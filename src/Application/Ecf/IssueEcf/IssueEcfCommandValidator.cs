using FluentValidation;
using NovaFE.Application.Ecf.Contracts;
using NovaFE.Domain.Common;
using NovaFE.Domain.Ecf;
using NovaFE.Domain.Fiscal;

namespace NovaFE.Application.Ecf.IssueEcf;

/// <summary>
/// Puerta de forma del payload de emisión: presencia, rangos, enums conocidos y
/// las reglas por tipo que conviene rechazar temprano con un mensaje claro. La
/// matriz de obligatoriedad completa y las invariantes fiscales las aplica
/// <see cref="EcfDocument.Create"/> (defensa en profundidad). Mensajes en español.
/// </summary>
public sealed class IssueEcfCommandValidator : AbstractValidator<IssueEcfCommand>
{
    private static readonly string KnownTypes =
        string.Join(", ", EcfType.GetAll().Select(type => type.Id));

    private static readonly int[] RetentionTypes = [41, 47];
    private static readonly int[] ReferenceTypes = [33, 34];

    public IssueEcfCommandValidator(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        RuleFor(x => x.Type)
            .Must(code => EcfType.FromCodeOrDefault(code) is not null)
            .WithMessage($"Tipo de comprobante desconocido. Valores válidos: {KnownTypes}.");

        RuleFor(x => x.IssueDate)
            .Must(date => date is null || date.Value <= timeProvider.GetDominicanToday())
            .WithMessage("La fecha de emisión no puede ser futura.");

        RuleFor(x => x.IncomeType)
            .Matches("^0[1-6]$").When(x => !string.IsNullOrWhiteSpace(x.IncomeType))
            .WithMessage("El tipo de ingresos debe ser '01' a '06'.");

        RuleFor(x => x.Lines)
            .NotEmpty().WithMessage("El comprobante necesita al menos una línea de detalle.")
            .Must(lines => lines.Count <= EcfDocument.MaxLines)
            .WithMessage($"El comprobante no puede tener más de {EcfDocument.MaxLines} líneas.");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.Name)
                .NotEmpty().WithMessage("Cada línea necesita un nombre de ítem.");
            line.RuleFor(l => l.Quantity)
                .GreaterThanOrEqualTo(0m).WithMessage("La cantidad no puede ser negativa.");
            line.RuleFor(l => l.UnitPrice)
                .GreaterThanOrEqualTo(0m).WithMessage("El precio unitario no puede ser negativo.");
            line.RuleFor(l => l.ItbisRate)
                .Must(rate => ItbisRate.FromIndicatorOrDefault(rate) is not null)
                .WithMessage("El indicador de facturación debe ser 1 (18 %), 2 (16 %), 3 (0 %) o 4 (exento).");
            line.RuleFor(l => l.Kind)
                .Must(kind => EcfPayloadEnum.Resolve<ItemKind>(kind) is not null)
                .WithMessage("El ítem debe ser un bien ('good') o un servicio ('service').");
        });

        RuleFor(x => x.Buyer!)
            .Must(buyer => string.IsNullOrWhiteSpace(buyer.Rnc) || string.IsNullOrWhiteSpace(buyer.ForeignId))
            .When(x => x.Buyer is not null)
            .WithMessage("El comprador se identifica con RNC o con identificador extranjero, no con ambos.");

        RuleFor(x => x.Payment.Condition)
            .Must(condition => EcfPayloadEnum.Resolve<PaymentCondition>(condition) is not null)
            .WithMessage("Condición de pago desconocida. Valores: 'cash', 'credit', 'free'.");

        RuleFor(x => x.Payment.DueDate)
            .NotNull()
            .When(x => string.Equals(x.Payment.Condition?.Trim(), "credit", StringComparison.OrdinalIgnoreCase)
                       || x.Payment.Condition?.Trim() == "2")
            .WithMessage("Una venta a crédito necesita fecha límite de pago.");

        RuleFor(x => x.ForeignCurrency!.ExchangeRate)
            .GreaterThan(0m).When(x => x.ForeignCurrency is not null)
            .WithMessage("El tipo de cambio debe ser mayor que cero.");

        // --- reglas por tipo (fail-fast; el dominio es la matriz autoritativa) ---

        RuleFor(x => x.Reference)
            .NotNull()
            .When(x => ReferenceTypes.Contains(x.Type))
            .WithMessage("Las Notas de Crédito (34) y de Débito (33) necesitan la sección de referencia al comprobante modificado.");

        RuleForEach(x => x.Lines)
            .Must(line => line.Retention is not null)
            .When(x => RetentionTypes.Contains(x.Type))
            .WithMessage("Cada línea de los tipos 41 (Compras) y 47 (Pagos al Exterior) necesita el área de retención.");
    }
}
