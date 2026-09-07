using ErrorOr;
using FluentValidation;
using NovaFE.Application.Common;
using NovaFE.Application.Common.Interfaces;
using NovaFE.Application.Webhooks.Contracts;
using NovaFE.Application.Webhooks.Interfaces;
using NovaFE.Domain.Common;
using NovaFE.Domain.Webhooks;
using Microsoft.Extensions.Logging;

namespace NovaFE.Application.Webhooks.UpdateEndpoint;

/// <summary>
/// Actualiza un endpoint. Si cambia la URL, se revalida contra la política
/// (https + anti-SSRF) antes de guardar.
/// </summary>
public sealed class UpdateWebhookEndpointUseCase(
    ILoggerFactory loggerFactory,
    IValidator<UpdateWebhookEndpointCommand> validator,
    ICurrentTenant currentTenant,
    IWebhookUrlPolicy urlPolicy,
    IWebhookEndpointRepository endpoints)
    : CommandUseCase<UpdateWebhookEndpointCommand, WebhookEndpointDto>(loggerFactory, validator)
{
    protected override async Task<ErrorOr<WebhookEndpointDto>> ExecuteCore(
        UpdateWebhookEndpointCommand request,
        CancellationToken ct)
    {
        if (!currentTenant.HasValue)
            return Errors.Auth.TenantNotResolved;

        var endpoint = await endpoints.GetAsync(request.Id, ct);
        if (endpoint is null)
            return WebhookEndpointErrors.NotFound(request.Id);

        if (request.Url is not null)
        {
            var allowed = await urlPolicy.EnsureAllowedAsync(request.Url, ct);
            if (allowed.IsError)
                return allowed.Errors;
        }

        var hasFieldChange = request.Url is not null || request.Events is not null || request.Description is not null;
        if (hasFieldChange)
        {
            var updated = endpoint.Update(request.Url, request.Events, request.Description);
            if (updated.IsError)
                return updated.Errors;
        }

        if (request.Enabled is { } enabled)
            endpoint.SetEnabled(enabled);
        else if (!hasFieldChange)
            return WebhookEndpointErrors.NothingToUpdate;

        await endpoints.UpdateAsync(endpoint, ct);

        return WebhookEndpointMapper.ToDto(endpoint);
    }
}
