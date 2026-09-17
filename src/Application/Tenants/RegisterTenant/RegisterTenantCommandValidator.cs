using FluentValidation;
using NovaFE.Domain.Common;

namespace NovaFE.Application.Tenants.RegisterTenant;

/// <summary>
/// Shape and presence checks. El RNC's exact format lives in <see cref="Rnc"/>,
/// checked here in a form usable without touching the DB. Messages are
/// consumer-facing, so Spanish.
/// </summary>
public sealed class RegisterTenantCommandValidator : AbstractValidator<RegisterTenantCommand>
{
    public RegisterTenantCommandValidator()
    {
        RuleFor(x => x.Rnc)
            .NotEmpty().WithMessage("El RNC es obligatorio.")
            .Must(rnc => Rnc.IsWellFormed(rnc.Trim()))
            .WithMessage("El RNC debe tener entre 9 y 11 dígitos, sin separadores.");

        RuleFor(x => x.LegalName)
            .NotEmpty().WithMessage("La razón social es obligatoria.")
            .MaximumLength(150).WithMessage("La razón social no puede exceder 150 caracteres.");

        RuleFor(x => x.TradeName)
            .MaximumLength(150).WithMessage("El nombre comercial no puede exceder 150 caracteres.");
    }
}
