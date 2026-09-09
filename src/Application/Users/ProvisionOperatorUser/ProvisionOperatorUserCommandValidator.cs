using FluentValidation;
using NovaFE.Domain.Users;

namespace NovaFE.Application.Users.ProvisionOperatorUser;

public sealed class ProvisionOperatorUserCommandValidator : AbstractValidator<ProvisionOperatorUserCommand>
{
    public ProvisionOperatorUserCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("El correo es obligatorio.")
            .MaximumLength(PlatformUser.MaxEmailLength)
            .WithMessage($"El correo admite hasta {PlatformUser.MaxEmailLength} caracteres.");
    }
}
