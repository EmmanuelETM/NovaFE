using ErrorOr;
using FluentValidation;
using NovaFE.Application.Common;
using NovaFE.Application.Common.Interfaces;
using NovaFE.Application.Webhooks.Contracts;
using NovaFE.Application.Webhooks.Interfaces;
using NovaFE.Domain.Common;
using NovaFE.Domain.Webhooks;
using Microsoft.Extensions.Logging;

namespace NovaFE.Application.Webhooks.CreateEndpoint;

/// <summary>
/// Registra un endpoint de webhook para el contribuyente de la petición. Aplica
/// el tope por tenant y la política de URL (https + anti-SSRF) antes de generar
/// el secret. La respuesta lleva el secret en claro — única vez que se ve.
/// </summary>
public sealed class CreateWebhookEndpointUseCase(
    ILoggerFactory loggerFactory,
    IValidator<CreateWebhookEndpointCommand> validator,
    ICurrentTenant currentTenant,
    WebhookSettings settings,
    IWebhookUrlPolicy urlPolicy,
    IWebhookEndpointRepository endpoints)
    : CommandUseCase<CreateWebhookEndpointCommand, WebhookEndpointCreatedDto>(loggerFactory, validator)
{
    protected override async Task<ErrorOr<WebhookEndpointCreatedDto>> ExecuteCore(
        CreateWebhookEndpointCommand request,
        CancellationToken ct)
    {
        if (currentTenant.TenantId is not { } tenantId)
            return Errors.Auth.TenantNotResolved;

        var count = await endpoints.CountAsync(ct);
        if (count >= settings.MaxEndpointsPerTenant)
            return WebhookEndpointErrors.LimitReached(settings.MaxEndpointsPerTenant);

        var allowed = await urlPolicy.EnsureAllowedAsync(request.Url ?? string.Empty, ct);
        if (allowed.IsError)
            return allowed.Errors;

        var secret = WebhookSecret.Generate();

        var created = WebhookEndpoint.Create(tenantId, request.Url, request.Events, request.Description, secret);
        if (created.IsError)
            return created.Errors;

        await endpoints.AddAsync(created.Value, ct);

        return new WebhookEndpointCreatedDto(WebhookEndpointMapper.ToDto(created.Value), secret);
    }
}
