using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ErrorOr;
using NovaFE.Application.Dgii.Contracts;
using NovaFE.Application.Dgii.Interfaces;
using NovaFE.Domain.Common;
using NovaFE.Domain.Common.Json;
using NovaFE.Domain.Dgii;
using NovaFE.Infrastructure.Http;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace NovaFE.Infrastructure.Dgii;

/// <summary>
/// Cliente HTTP de trackIds y directorio (Módulo 10). Usa el mismo cliente
/// resiliente <c>dgii-ecf</c> que <see cref="DgiiSubmissionClient"/> — mismo
/// dominio. No disponible en CerteCF (§5.10 de la Descripción Técnica de
/// Servicios DGII): ambos servicios devuelven
/// <see cref="DgiiQueryErrors.NotAvailableInEnvironment"/> sin llamar.
/// </summary>
internal sealed class DgiiQueryClient(IHttpClientFactory httpClientFactory) : IDgiiQueryClient
{
    public async Task<ErrorOr<IReadOnlyList<DgiiTrackIdEntry>>> GetTrackIdsAsync(
        DgiiEnvironment environment, string bearerToken, string rncEmisor, string encf, CancellationToken ct = default)
    {
        if (environment == DgiiEnvironment.Cert)
            return DgiiQueryErrors.NotAvailableInEnvironment("consultatrackids", environment);

        var path = $"{environment.UrlSegment}/consultatrackids/api/trackids/consulta" +
            $"?rncemisor={Uri.EscapeDataString(rncEmisor)}&encf={Uri.EscapeDataString(encf)}";

        try
        {
            using var response = await SendAsync(path, bearerToken, ct);
            if (!response.IsSuccessStatusCode)
                return DgiiQueryErrors.QueryFailed("consultatrackids", (int)response.StatusCode);

            var payload = await ReadJsonAsync<List<TrackIdWire>>(response, ct);
            if (payload is null)
                return DgiiQueryErrors.MalformedResponse;

            return ErrorOrFactory.From((IReadOnlyList<DgiiTrackIdEntry>)
                [.. payload.Select(w => new DgiiTrackIdEntry(w.TrackId ?? string.Empty, w.Estado ?? string.Empty, w.FechaRecepcion))]);
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

    public async Task<ErrorOr<IReadOnlyList<DgiiDirectoryEntry>>> ListDirectoryAsync(
        DgiiEnvironment environment, string bearerToken, CancellationToken ct = default)
    {
        if (environment == DgiiEnvironment.Cert)
            return DgiiQueryErrors.NotAvailableInEnvironment("consultadirectorio", environment);

        var path = $"{environment.UrlSegment}/consultadirectorio/api/consultas/listado";

        try
        {
            using var response = await SendAsync(path, bearerToken, ct);
            if (!response.IsSuccessStatusCode)
                return DgiiQueryErrors.QueryFailed("consultadirectorio", (int)response.StatusCode);

            var payload = await ReadJsonAsync<List<DirectoryWire>>(response, ct);
            if (payload is null)
                return DgiiQueryErrors.MalformedResponse;

            return ErrorOrFactory.From((IReadOnlyList<DgiiDirectoryEntry>)[.. payload.Select(ToEntry)]);
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

    public async Task<ErrorOr<DgiiDirectoryEntry?>> GetDirectoryEntryAsync(
        DgiiEnvironment environment, string bearerToken, string rnc, CancellationToken ct = default)
    {
        if (environment == DgiiEnvironment.Cert)
            return DgiiQueryErrors.NotAvailableInEnvironment("consultadirectorio", environment);

        var path = $"{environment.UrlSegment}/consultadirectorio/api/consultas/obtenerDirectorioporrnc" +
            $"?RNC={Uri.EscapeDataString(rnc)}";

        try
        {
            using var response = await SendAsync(path, bearerToken, ct);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return (DgiiDirectoryEntry?)null;
            if (!response.IsSuccessStatusCode)
                return DgiiQueryErrors.QueryFailed("consultadirectorio", (int)response.StatusCode);

            var payload = await ReadJsonAsync<DirectoryWire?>(response, ct);
            return payload is null ? (DgiiDirectoryEntry?)null : ToEntry(payload);
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

    private async Task<HttpResponseMessage> SendAsync(string path, string bearerToken, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

        using var http = httpClientFactory.CreateClient(DgiiSubmissionClient.EcfClientName);
        return await http.SendAsync(request, ct);
    }

    private static DgiiDirectoryEntry ToEntry(DirectoryWire w) => new(
        w.Rnc ?? string.Empty, w.Nombre ?? string.Empty, w.UrlRecepcion, w.UrlAceptacion, w.UrlOpcional);

    private static async Task<T?> ReadJsonAsync<T>(HttpResponseMessage response, CancellationToken ct)
        => await response.Content.ReadFromJsonAsync<T>(JsonSettings.Bulletproof, ct);

    private static bool IsTransportFailure(Exception ex) => ex
        is HttpRequestException
        or TaskCanceledException
        or TimeoutRejectedException
        or BrokenCircuitException;

    // --- DTOs del cable (nombres tal como los devuelve la DGII) ----------

    private sealed record TrackIdWire(
        [property: JsonPropertyName("trackId")] string? TrackId,
        [property: JsonPropertyName("estado")] string? Estado,
        [property: JsonPropertyName("fechaRecepcion")] DateTimeOffset? FechaRecepcion);

    private sealed record DirectoryWire(
        [property: JsonPropertyName("rnc")] string? Rnc,
        [property: JsonPropertyName("nombre")] string? Nombre,
        [property: JsonPropertyName("urlRecepcion")] string? UrlRecepcion,
        [property: JsonPropertyName("urlAceptacion")] string? UrlAceptacion,
        [property: JsonPropertyName("urlOpcional")] string? UrlOpcional);
}
