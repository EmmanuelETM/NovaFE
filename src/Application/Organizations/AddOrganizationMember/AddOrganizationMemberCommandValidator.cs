using FluentValidation;
using NovaFE.Domain.Organizations;

namespace NovaFE.Application.Organizations.AddOrganizationMember;

/// <summary>
/// Shape and presence checks. La existencia del usuario, la del duplicado y
/// las invariantes de dominio las resuelve el caso de uso.
/// </summary>
public sealed class AddOrganizationMemberCommandValidator : AbstractValidator<AddOrganizationMemberCommand>
{
    public AddOrganizationMemberCommandValidator()
    {
        RuleFor(x => x.OrganizationId)
            .NotEmpty().WithMessage("La organización es obligatoria.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("El correo es obligatorio.");

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
