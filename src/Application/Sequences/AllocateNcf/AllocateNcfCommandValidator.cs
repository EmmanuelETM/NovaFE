using FluentValidation;
using NovaFE.Domain.Common;

namespace NovaFE.Application.Sequences.AllocateNcf;

public sealed class AllocateNcfCommandValidator : AbstractValidator<AllocateNcfCommand>
{
    private static readonly string KnownEnvironments =
        string.Join(", ", DgiiEnvironment.GetAll().Select(e => e.Name));

    private static readonly string KnownTypes =
        string.Join(", ", EcfType.GetAll().Select(t => t.Id));

    public AllocateNcfCommandValidator()
    {
        RuleFor(x => x.Environment)
            .NotEmpty().WithMessage("El ambiente es obligatorio.")
            .Must(environment => DgiiEnvironment.GetAll()
                .Any(e => string.Equals(e.Name, environment?.Trim(), StringComparison.OrdinalIgnoreCase)))
            .WithMessage($"Ambiente desconocido. Valores válidos: {KnownEnvironments}.");

        RuleFor(x => x.Type)
            .Must(code => EcfType.FromCodeOrDefault(code) is not null)
            .WithMessage($"Tipo de comprobante desconocido. Valores válidos: {KnownTypes}.");
    }
}
