using ErrorOr;
using FluentValidation;
using Microsoft.Extensions.Logging;
using NovaFE.Application.Common;
using NovaFE.Application.Users.Contracts;
using NovaFE.Application.Users.Interfaces;
using NovaFE.Domain.Users;

namespace NovaFE.Application.Users.ProvisionOperatorUser;

/// <summary>Da de alta a un operador del SaaS. Recurso de operador.</summary>
public sealed class ProvisionOperatorUserUseCase(
    ILoggerFactory loggerFactory,
    IValidator<ProvisionOperatorUserCommand> validator,
    IPlatformUserRepository users)
    : CommandUseCase<ProvisionOperatorUserCommand, PlatformUserDto>(loggerFactory, validator)
{
    protected override async Task<ErrorOr<PlatformUserDto>> ExecuteCore(
        ProvisionOperatorUserCommand request,
        CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        if (await users.GetByEmailAsync(email, ct) is not null)
            return PlatformUserErrors.EmailAlreadyProvisioned(email);

        var created = PlatformUser.CreateOperator(request.Email);
        if (created.IsError)
            return created.Errors;

        await users.AddAsync(created.Value, ct);

        return UserDtoMapper.ToDto(created.Value);
    }
}
