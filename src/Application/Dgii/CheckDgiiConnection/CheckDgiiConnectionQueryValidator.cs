using FluentValidation;
using NovaFE.Domain.Common;

namespace NovaFE.Application.Dgii.CheckDgiiConnection;

public sealed class CheckDgiiConnectionQueryValidator : AbstractValidator<CheckDgiiConnectionQuery>
{
    private static readonly string KnownEnvironments =
        string.Join(", ", DgiiEnvironment.GetAll().Select(e => e.Name));

    public CheckDgiiConnectionQueryValidator()
    {
        RuleFor(x => x.Environment)
            .NotEmpty().WithMessage("El ambiente es obligatorio.")
            .Must(environment => DgiiEnvironment.GetAll()
                .Any(e => string.Equals(e.Name, environment?.Trim(), StringComparison.OrdinalIgnoreCase)))
            .WithMessage($"Ambiente desconocido. Valores válidos: {KnownEnvironments}.");
    }
}
