using FluentValidation;

namespace NovaFE.Application.Settings.UpdatePlatformSetting;

/// <summary>
/// Solo forma. La existencia de la clave, la deprecación y la validación del valor
/// contra la definición las hace el caso de uso (la definición es la matriz
/// autoritativa, igual que <c>EmitterProfile.Create</c>).
/// </summary>
public sealed class UpdatePlatformSettingCommandValidator : AbstractValidator<UpdatePlatformSettingCommand>
{
    public UpdatePlatformSettingCommandValidator()
    {
        RuleFor(x => x.Key)
            .NotEmpty().WithMessage("La clave del setting es obligatoria.");

        RuleFor(x => x.Value)
            .NotNull().WithMessage("El valor es obligatorio.");
    }
}
