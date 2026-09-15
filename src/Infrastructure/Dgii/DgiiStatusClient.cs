using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using ErrorOr;
using NovaFE.Application.Dgii.Contracts;
using NovaFE.Application.Dgii.Interfaces;
using NovaFE.Domain.Common;
using NovaFE.Domain.Dgii;
using NovaFE.Infrastructure.Http;
using Microsoft.Extensions.Options;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace NovaFE.Infrastructure.Dgii;

/// <summary>
/// Cliente del estatus de servicio de la DGII (Módulo 10) —
/// <c>statusecf.dgii.gov.do</c>. El <see cref="HttpClient"/> viene con
/// resiliencia estándar por <c>AddResilientHttpClient</c> (mismo patrón que
/// <see cref="DgiiAuthClient"/>) y su <c>BaseAddress</c> ya es el dominio de
/// estatus; aquí solo se arma el resto de la ruta. Auth
/// <c>Authorization: ApiKey {key}</c> (<see cref="DgiiOptions.StatusApiKey"/>,
/// distinta del Bearer del resto de servicios). El path, el método y el esquema
/// de auth de los tres endpoints están verificados contra el OpenAPI público del
/// servicio (<c>https://statusecf.dgii.gov.do/api-docs/v1/definition.json</c>);
/// el <b>schema de la respuesta no</b> — cada endpoint solo documenta
/// <c>"200": {"description": "Success"}</c>. Por eso el parseo es tolerante
/// (nombres de campo alternativos, todo opcional) y siempre guarda el JSON crudo
/// en el DTO. Ver <c>docs/dgii-queries.md</c>.
/// </summary>
internal sealed class DgiiStatusClient(HttpClient http, IOptions<DgiiOptions> options) : IDgiiStatusClient
{
    public Task<ErrorOr<IReadOnlyList<DgiiServiceStatus>>> GetServiceStatusAsync(CancellationToken ct = default)
        => GetListAsync(
            "/api/EstatusServicios/ObtenerEstatus",
            "ObtenerEstatus",
            el => new DgiiServiceStatus(
                Str(el, "nombre", "servicio", "name"),
                Bool(el, "disponible", "activo", "estado", "isAvailable"),
                el.GetRawText()),
            ct);

    public Task<ErrorOr<IReadOnlyList<DgiiMaintenanceWindow>>> GetMaintenanceWindowsAsync(CancellationToken ct = default)
        => GetListAsync(
            "/api/EstatusServicios/ObtenerVentanasMantenimiento",
            "ObtenerVentanasMantenimiento",
            el => new DgiiMaintenanceWindow(
                DateTimeOffsetValue(el, "inicio", "fechaInicio", "desde"),
                DateTimeOffsetValue(el, "fin", "fechaFin", "hasta"),
                el.GetRawText()),
            ct);

    public async Task<ErrorOr<DgiiEnvironmentStatus>> VerifyEnvironmentStatusAsync(
        DgiiEnvironment environment, CancellationToken ct = default)
    {
        var apiKey = options.Value.StatusApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
            return DgiiQueryErrors.StatusApiKeyNotConfigured;

        try
        {
            using var response = await SendAsync(
                $"/api/EstatusServicios/VerificarEstado?Ambiente={environment.Id}", apiKey, ct);
            if (!response.IsSuccessStatusCode)
                return DgiiQueryErrors.QueryFailed("VerificarEstado", (int)response.StatusCode);

            var raw = await response.Content.ReadAsStringAsync(ct);
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(raw) ? "{}" : raw);
            var root = document.RootElement;

            return new DgiiEnvironmentStatus(
                Bool(root, "enMantenimiento", "mantenimiento", "disponible"), root.GetRawText());
        }
        catch (Exception ex) when (IsTransportFailure(ex))
        {
            return HttpErrorMapper.Map(ex);
        }
        catch (JsonException)
        {
            return DgiiQueryErrors.MalformedResponse;
        }
    }

    private async Task<ErrorOr<IReadOnlyList<T>>> GetListAsync<T>(
        string path, string service, Func<JsonElement, T> map, CancellationToken ct)
    {
        var apiKey = options.Value.StatusApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
            return DgiiQueryErrors.StatusApiKeyNotConfigured;

        try
        {
            using var response = await SendAsync(path, apiKey, ct);
            if (!response.IsSuccessStatusCode)
                return DgiiQueryErrors.QueryFailed(service, (int)response.StatusCode);

            var raw = await response.Content.ReadAsStringAsync(ct);
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(raw) ? "[]" : raw);
            var root = document.RootElement;

            // Tolerante a que la DGII envuelva el array en un objeto (p. ej.
            // { "items": [...] }) en vez de devolverlo desnudo — se busca el
            // primer array que aparezca en el nivel superior.
            var items = root.ValueKind switch
            {
                JsonValueKind.Array => root,
                JsonValueKind.Object => FirstArrayProperty(root) ?? root,
                _ => root,
            };

            if (items.ValueKind != JsonValueKind.Array)
                return DgiiQueryErrors.MalformedResponse;

            return ErrorOrFactory.From((IReadOnlyList<T>)[.. items.EnumerateArray().Select(map)]);
        }
        catch (Exception ex) when (IsTransportFailure(ex))
        {
            return HttpErrorMapper.Map(ex);
        }
        catch (JsonException)
        {
            return DgiiQueryErrors.MalformedResponse;
        }
    }

    private async Task<HttpResponseMessage> SendAsync(string path, string apiKey, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("ApiKey", apiKey);

        return await http.SendAsync(request, ct);
    }

    private static JsonElement? FirstArrayProperty(JsonElement obj)
    {
        foreach (var property in obj.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.Array)
                return property.Value;
        }

        return null;
    }

    private static string? Str(JsonElement el, params string[] names)
    {
        foreach (var name in names)
        {
            if (el.ValueKind == JsonValueKind.Object && el.TryGetProperty(name, out var value)
                && value.ValueKind == JsonValueKind.String)
                return value.GetString();
        }

        return null;
    }

    private static bool? Bool(JsonElement el, params string[] names)
    {
        foreach (var name in names)
        {
            if (el.ValueKind != JsonValueKind.Object || !el.TryGetProperty(name, out var value))
                continue;

            if (value.ValueKind is JsonValueKind.True or JsonValueKind.False)
                return value.GetBoolean();
        }

        return null;
    }

    private static DateTimeOffset? DateTimeOffsetValue(JsonElement el, params string[] names)
    {
        foreach (var name in names)
        {
            if (el.ValueKind == JsonValueKind.Object && el.TryGetProperty(name, out var value)
                && value.ValueKind == JsonValueKind.String
                && DateTimeOffset.TryParse(value.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                return parsed;
        }

        return null;
    }

    private static bool IsTransportFailure(Exception ex) => ex
        is HttpRequestException
        or TaskCanceledException
        or TimeoutRejectedException
        or BrokenCircuitException;
}
