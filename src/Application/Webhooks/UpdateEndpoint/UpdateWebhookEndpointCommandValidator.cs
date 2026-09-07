using FluentValidation;
using NovaFE.Domain.Webhooks;

namespace NovaFE.Application.Webhooks.UpdateEndpoint;

/// <summary>
/// Solo forma de los campos que vengan. El resto (https, anti-SSRF, "nada que
/// cambiar") lo resuelven el caso de uso y <see cref="WebhookEndpoint"/>.
/// </summary>
public sealed class UpdateWebhookEndpointCommandValidator : AbstractValidator<UpdateWebhookEndpointCommand>
{
    public UpdateWebhookEndpointCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();

        RuleFor(x => x.Url)
            .MaximumLength(WebhookEndpoint.MaxUrlLength)
            .Must(BeAnAbsoluteHttpUrl!)
            .When(x => !string.IsNullOrWhiteSpace(x.Url))
            .WithMessage("La URL de destino debe ser absoluta y usar http o https.");

        RuleForEach(x => x.Events)
            .Must(WebhookEventType.IsValidSubscription)
            .When(x => x.Events is not null)
            .WithMessage(e => $"'{e}' no es un evento válido. Usá un tipo exacto (p. ej. 'ecf.accepted'), 'ecf.*' o '*'.");

        RuleFor(x => x.Events)
            .Must(e => e is null || e.Count > 0)
            .WithMessage("Si se envía la lista de eventos, debe tener al menos uno.");

        RuleFor(x => x.Description)
            .MaximumLength(WebhookEndpoint.MaxDescriptionLength)
            .When(x => x.Description is not null)
            .WithMessage($"La descripción admite hasta {WebhookEndpoint.MaxDescriptionLength} caracteres.");
    }

    private static bool BeAnAbsoluteHttpUrl(string url) =>
        Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
