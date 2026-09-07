using FluentValidation;
using NovaFE.Domain.Webhooks;

namespace NovaFE.Application.Webhooks.CreateEndpoint;

/// <summary>
/// Forma y presencia. La regla de <c>https</c> obligatorio y el guard anti-SSRF
/// (E/S) los aplica el caso de uso vía <c>IWebhookUrlPolicy</c>; las invariantes
/// viven en <see cref="WebhookEndpoint"/>. Mensajes en español.
/// </summary>
public sealed class CreateWebhookEndpointCommandValidator : AbstractValidator<CreateWebhookEndpointCommand>
{
    public CreateWebhookEndpointCommandValidator()
    {
        RuleFor(x => x.Url)
            .NotEmpty().WithMessage("La URL de destino es obligatoria.")
            .MaximumLength(WebhookEndpoint.MaxUrlLength)
            .Must(BeAnAbsoluteHttpUrl!)
            .When(x => !string.IsNullOrWhiteSpace(x.Url))
            .WithMessage("La URL de destino debe ser absoluta y usar http o https.");

        RuleFor(x => x.Events)
            .NotEmpty().WithMessage("Hay que suscribirse al menos a un evento.");

        RuleForEach(x => x.Events)
            .Must(WebhookEventType.IsValidSubscription)
            .When(x => x.Events is not null)
            .WithMessage(e => $"'{e}' no es un evento válido. Usá un tipo exacto (p. ej. 'ecf.accepted'), 'ecf.*' o '*'.");

        RuleFor(x => x.Description)
            .MaximumLength(WebhookEndpoint.MaxDescriptionLength)
            .WithMessage($"La descripción admite hasta {WebhookEndpoint.MaxDescriptionLength} caracteres.");
    }

    private static bool BeAnAbsoluteHttpUrl(string url) =>
        Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
