using FluentValidation;
using NovaFE.Domain.Common;

namespace NovaFE.Application.Certificates.UploadCertificate;

public sealed class UploadCertificateCommandValidator : AbstractValidator<UploadCertificateCommand>
{
    private const int MaxSizeBytes = 64 * 1024;

    private static readonly string KnownEnvironments =
        string.Join(", ", DgiiEnvironment.GetAll().Select(e => e.Name));

    public UploadCertificateCommandValidator()
    {
        RuleFor(x => x.Content)
            .NotNull().WithMessage("El archivo del certificado es obligatorio.")
            .Must(content => content is { Length: > 0 })
            .WithMessage("El archivo del certificado está vacío.")
            .Must(content => content is null || content.Length <= MaxSizeBytes)
            .WithMessage($"El archivo del certificado no puede exceder {MaxSizeBytes / 1024} KB.");

        RuleFor(x => x.Password)
            .NotNull().WithMessage("La contraseña del certificado es obligatoria (puede ser una cadena vacía).");

        RuleFor(x => x.Environment)
            .NotEmpty().WithMessage("El ambiente es obligatorio.")
            .Must(BeAKnownEnvironment)
            .WithMessage($"Ambiente desconocido. Valores válidos: {KnownEnvironments}.");
    }

    private static bool BeAKnownEnvironment(string environment)
        => DgiiEnvironment.GetAll()
            .Any(e => string.Equals(e.Name, environment?.Trim(), StringComparison.OrdinalIgnoreCase));
}
