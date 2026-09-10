using FluentValidation;
using NovaFE.Domain.Users;

namespace NovaFE.Application.Users.ChangeUserRole;

/// <summary>
/// Forma y presencia. La existencia del usuario y su alcance por contribuyente
/// las comprueba el caso de uso; las invariantes viven en <see cref="PlatformUser"/>.
/// </summary>
public sealed class ChangeUserRoleCommandValidator : AbstractValidator<ChangeUserRoleCommand>
{
    public ChangeUserRoleCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("El usuario es obligatorio.");

        RuleFor(x => x.TenantScope)
            .NotEmpty().WithMessage("El contribuyente es obligatorio.");

        RuleFor(x => x.Role)
            .NotEmpty().WithMessage($"El rol es obligatorio. Valores válidos: {ValidTenantRoles}.");

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
