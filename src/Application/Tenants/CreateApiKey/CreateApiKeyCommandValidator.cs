using FluentValidation;
using NovaFE.Domain.Tenants;

namespace NovaFE.Application.Tenants.CreateApiKey;

/// <summary>
/// Forma y presencia. La existencia del contribuyente la comprueba el caso de
/// uso; las invariantes viven en <see cref="ApiKey"/>. Mensajes en español.
/// </summary>
public sealed class CreateApiKeyCommandValidator : AbstractValidator<CreateApiKeyCommand>
{
    public CreateApiKeyCommandValidator(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("El contribuyente es obligatorio.");

        RuleFor(x => x.Label)
            .MaximumLength(ApiKey.MaxLabelLength)
            .WithMessage($"La etiqueta admite hasta {ApiKey.MaxLabelLength} caracteres.");

        RuleFor(x => x.ExpiresAt)
            .Must(expiresAt => expiresAt is null || expiresAt > timeProvider.GetUtcNow())
            .WithMessage("La fecha de vencimiento debe ser futura.");
    }
}
