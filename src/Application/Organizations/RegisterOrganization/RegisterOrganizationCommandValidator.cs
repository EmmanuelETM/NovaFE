using System.Text.RegularExpressions;
using FluentValidation;
using NovaFE.Domain.Organizations;

namespace NovaFE.Application.Organizations.RegisterOrganization;

/// <summary>
/// Shape and presence checks. La unicidad del slug y la existencia/alta del
/// dueño las resuelve el caso de uso (necesitan E/S). Mensajes de cara al
/// consumidor de la API, por eso en español.
/// </summary>
public sealed partial class RegisterOrganizationCommandValidator : AbstractValidator<RegisterOrganizationCommand>
{
    public RegisterOrganizationCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(Organization.MaxNameLength)
            .WithMessage($"El nombre no puede exceder {Organization.MaxNameLength} caracteres.");

        RuleFor(x => x.Slug)
            .NotEmpty().WithMessage("El slug es obligatorio.")
            .MaximumLength(Organization.MaxSlugLength)
            .WithMessage($"El slug no puede exceder {Organization.MaxSlugLength} caracteres.")
            .Must(slug => SlugFormat().IsMatch(slug.Trim()))
            .WithMessage("El slug solo admite minúsculas, dígitos y guiones (sin empezar ni terminar en guion).");

        RuleFor(x => x.Plan)
            .NotEmpty().WithMessage("El plan es obligatorio.")
            .Must(BeAKnownPlan)
            .WithMessage($"Plan desconocido. Valores válidos: {KnownPlans}.");

        RuleFor(x => x.OwnerEmail)
            .MaximumLength(Domain.Users.PlatformUser.MaxEmailLength)
            .When(x => x.OwnerEmail is not null)
            .WithMessage($"El correo admite hasta {Domain.Users.PlatformUser.MaxEmailLength} caracteres.");
    }

    private static readonly string KnownPlans =
        string.Join(", ", OrganizationPlan.GetAll().Select(p => p.Name));

    private static bool BeAKnownPlan(string plan)
        => OrganizationPlan.GetAll().Any(p => string.Equals(p.Name, plan?.Trim(), StringComparison.OrdinalIgnoreCase));

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex SlugFormat();
}
