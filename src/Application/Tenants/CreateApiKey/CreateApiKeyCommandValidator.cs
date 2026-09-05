using FluentValidation;
using NovaFE.Domain.Common;
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

        RuleFor(x => x.Environment)
            .Must(BeAKnownEnvironment)
            .When(x => !string.IsNullOrWhiteSpace(x.Environment))
            .WithMessage($"Ambiente desconocido. Valores válidos: {string.Join(", ", DgiiEnvironment.GetAll().Select(e => e.Name))}.");

        // Dos reglas separadas (no encadenadas): un `.When()` sobre una cadena
        // aplica, por defecto, a *todos* los validadores anteriores de esa misma
        // cadena — habría apagado el `NotEmpty()` de abajo también.
        RuleFor(x => x.Role)
            .NotEmpty().WithMessage($"El rol es obligatorio. Valores válidos: {string.Join(", ", ApiKeyRole.GetAll().Select(r => r.Name))}.");

        RuleFor(x => x.Role)
            .Must(BeAKnownApiKeyRole)
            .When(x => !string.IsNullOrWhiteSpace(x.Role))
            .WithMessage($"Rol desconocido. Valores válidos: {string.Join(", ", ApiKeyRole.GetAll().Select(r => r.Name))}.");

        RuleFor(x => x.ExpiresAt)
            .Must(expiresAt => expiresAt is null || expiresAt > timeProvider.GetUtcNow())
            .WithMessage("La fecha de vencimiento debe ser futura.");
    }

    private static bool BeAKnownEnvironment(string? environment) =>
        DgiiEnvironment.GetAll()
            .Any(e => string.Equals(e.Name, environment?.Trim(), StringComparison.OrdinalIgnoreCase));

    // admin_sistema es exclusivo del operador (otro esquema de auth) — no es un
    // rol válido para una API key de contribuyente.
    private static bool BeAKnownApiKeyRole(string? role) =>
        ApiKeyRole.GetAll().Any(r => string.Equals(r.Name, role?.Trim(), StringComparison.OrdinalIgnoreCase));
}
