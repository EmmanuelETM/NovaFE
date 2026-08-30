using FluentValidation;
using NovaFE.Domain.Common;
using NovaFE.Domain.Tenants;

namespace NovaFE.Application.Tenants.RegisterTenant;

/// <summary>
/// Shape and presence checks. The RNC's exact format lives in <see cref="Rnc"/>
/// and the plan is resolved in the use case; here we only fail fast on obvious
/// bad input. Messages are consumer-facing, so Spanish.
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

        RuleFor(x => x.Plan)
            .NotEmpty().WithMessage("El plan es obligatorio.")
            .Must(BeAKnownPlan)
            .WithMessage($"Plan desconocido. Valores válidos: {KnownPlans}.");
    }

    private static readonly string KnownPlans =
        string.Join(", ", TenantPlan.GetAll().Select(p => p.Name));

    private static bool BeAKnownPlan(string plan)
        => TenantPlan.GetAll().Any(p => string.Equals(p.Name, plan?.Trim(), StringComparison.OrdinalIgnoreCase));
}
