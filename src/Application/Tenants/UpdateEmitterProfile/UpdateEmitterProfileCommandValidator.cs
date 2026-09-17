using FluentValidation;
using NovaFE.Domain.Tenants;

namespace NovaFE.Application.Tenants.UpdateEmitterProfile;

/// <summary>
/// Forma y presencia. Las invariantes del perfil viven en
/// <see cref="EmitterProfile"/>; que ya exista un perfil para actualizar lo
/// comprueba el caso de uso. Mensajes de cara al cliente, en español.
/// </summary>
public sealed class UpdateEmitterProfileCommandValidator : AbstractValidator<UpdateEmitterProfileCommand>
{
    public UpdateEmitterProfileCommandValidator()
    {
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
    }
}
