using FluentValidation;

namespace NovaFE.Application.Finance.GetFiscalSummary;

/// <summary>Forma: el rango de fechas tiene que existir y venir en orden. Sin E/S.</summary>
public sealed class GetFiscalSummaryQueryValidator : AbstractValidator<GetFiscalSummaryQuery>
{
    public GetFiscalSummaryQueryValidator()
    {
        RuleFor(x => x.To)
            .GreaterThanOrEqualTo(x => x.From)
            .WithMessage("La fecha final no puede ser anterior a la fecha inicial.");
    }
}
