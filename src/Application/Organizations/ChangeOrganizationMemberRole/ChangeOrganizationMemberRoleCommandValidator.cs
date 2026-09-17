using FluentValidation;
using NovaFE.Domain.Organizations;

namespace NovaFE.Application.Organizations.ChangeOrganizationMemberRole;

public sealed class ChangeOrganizationMemberRoleCommandValidator : AbstractValidator<ChangeOrganizationMemberRoleCommand>
{
    public ChangeOrganizationMemberRoleCommandValidator()
    {
        RuleFor(x => x.OrganizationId)
            .NotEmpty().WithMessage("La organización es obligatoria.");

        RuleFor(x => x.PlatformUserId)
            .NotEmpty().WithMessage("El usuario es obligatorio.");

        RuleFor(x => x.Role)
            .NotEmpty().WithMessage($"El rol es obligatorio. Valores válidos: {ValidRoles}.")
            .Must(BeAKnownRole)
            .When(x => !string.IsNullOrWhiteSpace(x.Role))
            .WithMessage($"Rol desconocido. Valores válidos: {ValidRoles}.");
    }

    private static string ValidRoles =>
        string.Join(", ", OrganizationRole.GetAll().Select(r => r.Name));

    private static bool BeAKnownRole(string role) =>
        OrganizationRole.GetAll().Any(r => string.Equals(r.Name, role.Trim(), StringComparison.OrdinalIgnoreCase));
}
