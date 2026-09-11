using FluentValidation;

namespace NovaFE.Application.Settings.UpdateTenantSetting;

/// <summary>
/// Solo forma. La existencia de la clave (que además sea de tenant y editable), la
/// deprecación y la validación del valor contra la definición las hace el caso de
/// uso — la definición es la matriz autoritativa.
/// </summary>
public sealed class UpdateTenantSettingCommandValidator : AbstractValidator<UpdateTenantSettingCommand>
{
    public UpdateTenantSettingCommandValidator()
    {
        RuleFor(x => x.Key)
            .NotEmpty().WithMessage("La clave del setting es obligatoria.");

        RuleFor(x => x.Value)
            .NotNull().WithMessage("El valor es obligatorio.");
    }
}
