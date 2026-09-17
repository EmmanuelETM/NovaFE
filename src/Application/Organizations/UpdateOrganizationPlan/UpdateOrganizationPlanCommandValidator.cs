using FluentValidation;
using NovaFE.Domain.Organizations;

namespace NovaFE.Application.Organizations.UpdateOrganizationPlan;

/// <summary>Shape and presence checks. La existencia de la organización la resuelve el caso de uso (necesita E/S).</summary>
public sealed class UpdateOrganizationPlanCommandValidator : AbstractValidator<UpdateOrganizationPlanCommand>
{
    public UpdateOrganizationPlanCommandValidator()
    {
        RuleFor(x => x.Plan)
            .NotEmpty().WithMessage("El plan es obligatorio.")
            .Must(BeAKnownPlan)
            .WithMessage($"Plan desconocido. Valores válidos: {KnownPlans}.");
    }

    private static readonly string KnownPlans =
        string.Join(", ", OrganizationPlan.GetAll().Select(p => p.Name));

    private static bool BeAKnownPlan(string plan)
        => OrganizationPlan.GetAll().Any(p => string.Equals(p.Name, plan?.Trim(), StringComparison.OrdinalIgnoreCase));
}
