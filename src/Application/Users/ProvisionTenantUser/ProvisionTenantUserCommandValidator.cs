using FluentValidation;
using NovaFE.Domain.Users;

namespace NovaFE.Application.Users.ProvisionTenantUser;

/// <summary>
/// Forma y presencia. La existencia del contribuyente y la unicidad del correo
/// las comprueba el caso de uso; las invariantes viven en <see cref="PlatformUser"/>.
/// </summary>
public sealed class ProvisionTenantUserCommandValidator : AbstractValidator<ProvisionTenantUserCommand>
{
    public ProvisionTenantUserCommandValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("El contribuyente es obligatorio.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("El correo es obligatorio.")
            .MaximumLength(PlatformUser.MaxEmailLength)
            .WithMessage($"El correo admite hasta {PlatformUser.MaxEmailLength} caracteres.");

        RuleFor(x => x.Role)
            .NotEmpty().WithMessage(
                $"El rol es obligatorio. Valores válidos: {ValidTenantRoles}.");

        RuleFor(x => x.Role)
            .Must(BeATenantRole)
            .When(x => !string.IsNullOrWhiteSpace(x.Role))
            .WithMessage($"Rol desconocido o no permitido. Valores válidos: {ValidTenantRoles}.");
    }

    private static string ValidTenantRoles =>
        string.Join(", ", PlatformRole.GetAll().Where(r => r.IsTenantRole).Select(r => r.Name));

    private static bool BeATenantRole(string? role) =>
        PlatformRole.GetAll().Any(r =>
            r.IsTenantRole && string.Equals(r.Name, role?.Trim(), StringComparison.OrdinalIgnoreCase));
}
