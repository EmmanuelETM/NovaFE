using FluentValidation;
using NovaFE.Domain.Common;
using NovaFE.Domain.Tenants;

namespace NovaFE.Application.Tenants.SetEmitterProfile;

/// <summary>
/// Forma y presencia. Las invariantes del perfil viven en
/// <see cref="EmitterProfile"/>; la existencia del contribuyente la comprueba el
/// caso de uso. Mensajes de cara al cliente, en español.
/// </summary>
public sealed class SetEmitterProfileCommandValidator : AbstractValidator<SetEmitterProfileCommand>
{
    private static readonly string KnownEnvironments =
        string.Join(", ", DgiiEnvironment.GetAll().Select(e => e.Name));

    public SetEmitterProfileCommandValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("El contribuyente es obligatorio.");

        RuleFor(x => x.Address)
            .NotEmpty().WithMessage("La dirección del emisor es obligatoria.")
            .MaximumLength(100).WithMessage("La dirección no puede exceder 100 caracteres.");

        RuleFor(x => x.Municipality)
            .MaximumLength(10).WithMessage("El municipio es un código de la Tabla III (máx. 10 caracteres).");

        RuleFor(x => x.Province)
            .MaximumLength(10).WithMessage("La provincia es un código de la Tabla III (máx. 10 caracteres).");

        RuleFor(x => x.Email)
            .MaximumLength(100).WithMessage("El correo no puede exceder 100 caracteres.")
            .EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email))
            .WithMessage("El correo del emisor no tiene un formato válido.");

        RuleFor(x => x.EconomicActivity)
            .MaximumLength(150).WithMessage("La actividad económica no puede exceder 150 caracteres.");

        RuleFor(x => x.Phones)
            .Must(phones => phones is null || phones.Count <= EmitterProfile.MaxPhones)
            .WithMessage($"El emisor admite hasta {EmitterProfile.MaxPhones} teléfonos.");

        RuleFor(x => x.DefaultEnvironment)
            .NotEmpty().WithMessage("El ambiente por defecto es obligatorio.")
            .Must(BeAKnownEnvironment)
            .WithMessage($"Ambiente desconocido. Valores válidos: {KnownEnvironments}.");
    }

    private static bool BeAKnownEnvironment(string environment) =>
        DgiiEnvironment.GetAll()
            .Any(e => string.Equals(e.Name, environment?.Trim(), StringComparison.OrdinalIgnoreCase));
}
