using ErrorOr;

namespace NovaFE.Application.Webhooks.Interfaces;

/// <summary>
/// Valida que una URL de webhook sea entregable: esquema permitido, <c>https</c>
/// si la configuración lo exige, y que no resuelva a una dirección
/// privada / loopback / link-local (guard anti-SSRF). Hace E/S (DNS), por eso
/// vive fuera del <c>AbstractValidator</c>. Se re-ejecuta antes de cada entrega
/// para cubrir el DNS rebinding.
/// </summary>
public interface IWebhookUrlPolicy
{
    Task<ErrorOr<Success>> EnsureAllowedAsync(string url, CancellationToken ct = default);
}
