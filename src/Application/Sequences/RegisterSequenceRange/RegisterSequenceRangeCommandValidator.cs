using FluentValidation;
using NovaFE.Domain.Common;
using NovaFE.Domain.Sequences;

namespace NovaFE.Application.Sequences.RegisterSequenceRange;

public sealed class RegisterSequenceRangeCommandValidator : AbstractValidator<RegisterSequenceRangeCommand>
{
    private static readonly string KnownEnvironments =
        string.Join(", ", DgiiEnvironment.GetAll().Select(e => e.Name));

    private static readonly string KnownTypes =
        string.Join(", ", EcfType.GetAll().Select(t => t.Id));

    public RegisterSequenceRangeCommandValidator()
    {
        RuleFor(x => x.Environment)
            .NotEmpty().WithMessage("El ambiente es obligatorio.")
            .Must(BeAKnownEnvironment)
            .WithMessage($"Ambiente desconocido. Valores válidos: {KnownEnvironments}.");

        RuleFor(x => x.Type)
            .Must(code => EcfType.FromCodeOrDefault(code) is not null)
            .WithMessage($"Tipo de comprobante desconocido. Valores válidos: {KnownTypes}.");

        RuleFor(x => x.Series)
            .NotEmpty().WithMessage("La serie es obligatoria.")
            .Must(series => series?.Trim().Length == 1 && Encf.IsValidSeries(char.ToUpperInvariant(series.Trim()[0])))
            .WithMessage("La serie debe ser una sola letra de la E a la Z, excepto la P.");

        RuleFor(x => x.RangeFrom)
            .GreaterThanOrEqualTo(1).WithMessage("El secuencial inicial ('desde') debe ser mayor o igual a 1.");

        RuleFor(x => x.RangeTo)
            .GreaterThanOrEqualTo(x => x.RangeFrom)
            .WithMessage("El secuencial final ('hasta') debe ser mayor o igual al inicial ('desde').");
    }

    private static bool BeAKnownEnvironment(string environment) =>
        DgiiEnvironment.GetAll()
            .Any(e => string.Equals(e.Name, environment?.Trim(), StringComparison.OrdinalIgnoreCase));
}
