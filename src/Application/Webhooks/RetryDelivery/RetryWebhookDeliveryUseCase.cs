using ErrorOr;
using NovaFE.Application.Common;
using NovaFE.Application.Webhooks.Interfaces;
using NovaFE.Domain.Webhooks;
using Microsoft.Extensions.Logging;

namespace NovaFE.Application.Webhooks.RetryDelivery;

public sealed class RetryWebhookDeliveryUseCase(
    ILoggerFactory loggerFactory,
    IWebhookOutbox outbox)
    : CommandUseCase<RetryWebhookDeliveryCommand>(loggerFactory)
{
    protected override async Task<ErrorOr<Success>> ExecuteCore(
        RetryWebhookDeliveryCommand request,
        CancellationToken ct)
    {
        var retried = await outbox.RetryAsync(request.DeliveryId, request.TenantId, ct);
        if (!retried)
            return WebhookDeliveryErrors.NotFound(request.DeliveryId);

        return Result.Success;
    }
}
