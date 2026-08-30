using System.Globalization;
using FluentValidation;
using NovaFE.Domain.Common;
using NovaFE.Domain.Sequences;

namespace NovaFE.Application.Sequences.RegisterSequenceRange;

/// <summary>
/// Puerta de entrada del comando: forma, rangos, enum conocido, reglas
/// condicionales por ambiente y la fecha de autorización. Todo lo que no dependa
/// de la base se valida aquí; si algo no cuadra, el caso de uso ni se ejecuta.
/// </summary>
public sealed class RegisterSequenceRangeCommandValidator : AbstractValidator<RegisterSequenceRangeCommand>
{
    /// <summary>Tope de secuencias por tipo en CerteCF (RF-07, tabla por ambiente).</summary>
    private const long CertEcfMaxSequential = 10_000_000;

    private static readonly string KnownEnvironments =
        string.Join(", ", DgiiEnvironment.GetAll().Select(e => e.Name));

    private static readonly string KnownTypes =
        string.Join(", ", EcfType.GetAll().Select(t => t.Id));

    private static readonly string CertEcfMaxSequentialText =
        CertEcfMaxSequential.ToString("N0", CultureInfo.InvariantCulture);

    public RegisterSequenceRangeCommandValidator(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

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

        RuleFor(x => x.AuthorizedOn)
            .Must(authorizedOn => authorizedOn is null || authorizedOn.Value <= timeProvider.GetDominicanToday())
            .WithMessage("La fecha de autorización no puede ser futura.");

        // En CerteCF el sistema siempre parte de 1 y el rango por tipo tiene tope.
        When(IsCertEcf, () =>
        {
            RuleFor(x => x.RangeFrom)
                .Equal(1).WithMessage("En CerteCF las secuencias siempre empiezan en 1.");

            RuleFor(x => x.RangeTo)
                .LessThanOrEqualTo(CertEcfMaxSequential)
                .WithMessage($"En CerteCF el rango por tipo no puede exceder {CertEcfMaxSequentialText} secuencias.");
        });
    }

    private static bool BeAKnownEnvironment(string environment) =>
        DgiiEnvironment.GetAll()
            .Any(e => string.Equals(e.Name, environment?.Trim(), StringComparison.OrdinalIgnoreCase));

    private static bool IsCertEcf(RegisterSequenceRangeCommand command) =>
        string.Equals(command.Environment?.Trim(), DgiiEnvironment.CertEcf.Name, StringComparison.OrdinalIgnoreCase);
}
