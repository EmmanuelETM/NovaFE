namespace NovaFE.Application.Webhooks.UpdateEndpoint;

/// <summary>
/// Actualización parcial de un endpoint. Un campo <c>null</c> se deja como está;
/// para borrar la descripción se manda cadena vacía. <see cref="Enabled"/>
/// habilita o deshabilita (al habilitar se reinicia el contador de fallos).
/// </summary>
public sealed record UpdateWebhookEndpointCommand(
    Guid Id,
    string? Url,
    IReadOnlyList<string>? Events,
    bool? Enabled,
    string? Description);
