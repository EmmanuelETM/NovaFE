using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
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
/// Cliente HTTP de recepción y consulta de resultado de la DGII. Usa dos clientes
/// resilientes con nombre: <c>dgii-ecf</c> (dominio de e-CF, para
/// <c>facturaselectronicas</c> y <c>consultaresultado</c>) y <c>dgii-fc</c>
/// (dominio de Facturas de Consumo, para <c>recepcionfc</c>). El token Bearer del
/// tenant lo pasa el llamador (lo resuelve <see cref="IDgiiTokenProvider"/>).
/// </summary>
internal sealed class DgiiSubmissionClient(IHttpClientFactory httpClientFactory) : IDgiiSubmissionClient
{
    internal const string EcfClientName = "dgii-ecf";
    internal const string FcClientName = "dgii-fc";

    public async Task<ErrorOr<DgiiSubmissionReceipt>> SubmitEcfAsync(
        DgiiEnvironment environment, string bearerToken, string signedXml, string encf, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(signedXml);

        try
        {
            using var request = XmlUpload(
                HttpMethod.Post,
                $"{environment.UrlSegment}/recepcion/api/facturaselectronicas",
                bearerToken, signedXml, encf);

            using var http = httpClientFactory.CreateClient(EcfClientName);
            using var response = await http.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
                return DgiiSubmissionErrors.ReceptionFailed((int)response.StatusCode);

            var payload = await ReadJsonAsync<EcfReceiptWire>(response, ct);
            if (payload is null)
                return DgiiSubmissionErrors.MalformedResponse;

            if (string.IsNullOrWhiteSpace(payload.TrackId))
                return DgiiSubmissionErrors.NoTrackId(payload.Mensaje ?? payload.Error);

            return new DgiiSubmissionReceipt(payload.TrackId.Trim(), payload.Mensaje);
        }
        catch (Exception ex) when (IsTransportFailure(ex))
        {
            return HttpErrorMapper.Map(ex);
        }
        catch (JsonException)
        {
            return DgiiSubmissionErrors.MalformedResponse;
        }
    }

    public async Task<ErrorOr<DgiiRfceReceipt>> SubmitRfceAsync(
        DgiiEnvironment environment, string bearerToken, string signedRfceXml, string encf, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(signedRfceXml);

        try
        {
            using var request = XmlUpload(
                HttpMethod.Post,
                $"{environment.UrlSegment}/recepcionfc/api/recepcion/ecf",
                bearerToken, signedRfceXml, encf);

            using var http = httpClientFactory.CreateClient(FcClientName);
            using var response = await http.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
                return DgiiSubmissionErrors.ReceptionFailed((int)response.StatusCode);

            var payload = await ReadJsonAsync<RfceResultWire>(response, ct);
            if (payload is null)
                return DgiiSubmissionErrors.MalformedResponse;

            return new DgiiRfceReceipt(
                payload.Codigo,
                payload.Estado ?? string.Empty,
                ToMessages(payload.Mensajes),
                payload.Encf,
                payload.SecuenciaUtilizada);
        }
        catch (Exception ex) when (IsTransportFailure(ex))
        {
            return HttpErrorMapper.Map(ex);
        }
        catch (JsonException)
        {
            return DgiiSubmissionErrors.MalformedResponse;
        }
    }

    public async Task<ErrorOr<DgiiEcfResult>> GetResultAsync(
        DgiiEnvironment environment, string bearerToken, string trackId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(trackId);

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"{environment.UrlSegment}/consultaresultado/api/consultas/estado?trackid={Uri.EscapeDataString(trackId)}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

            using var http = httpClientFactory.CreateClient(EcfClientName);
            using var response = await http.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
                return DgiiSubmissionErrors.ResultQueryFailed((int)response.StatusCode);

            var payload = await ReadJsonAsync<EcfResultWire>(response, ct);
            if (payload is null)
                return DgiiSubmissionErrors.MalformedResponse;

            return new DgiiEcfResult(
                payload.Codigo,
                payload.Estado ?? string.Empty,
                ToMessages(payload.Mensajes),
                payload.SecuenciaUtilizada,
                payload.FechaRecepcion);
        }
        catch (Exception ex) when (IsTransportFailure(ex))
        {
            return HttpErrorMapper.Map(ex);
        }
        catch (JsonException)
        {
            return DgiiSubmissionErrors.MalformedResponse;
        }
    }

    private static HttpRequestMessage XmlUpload(
        HttpMethod method, string path, string bearerToken, string xml, string encf)
    {
        var form = new MultipartFormDataContent();
        var xmlPart = new StringContent(xml, new UTF8Encoding(false), "application/xml");
        form.Add(xmlPart, name: "xml", fileName: $"{encf}.xml");

        return new HttpRequestMessage(method, path)
        {
            Content = form,
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", bearerToken) },
        };
    }

    private static async Task<T?> ReadJsonAsync<T>(HttpResponseMessage response, CancellationToken ct)
        => await response.Content.ReadFromJsonAsync<T>(JsonSettings.Bulletproof, ct);

    private static IReadOnlyList<DgiiMessage> ToMessages(IReadOnlyList<MessageWire>? messages)
        => messages is null or { Count: 0 }
            ? []
            : [.. messages.Select(m => new DgiiMessage(m.CodeAsInt, m.Valor ?? string.Empty))];

    private static bool IsTransportFailure(Exception ex) => ex
        is HttpRequestException
        or TaskCanceledException
        or TimeoutRejectedException
        or BrokenCircuitException;

    // --- DTOs del cable (nombres tal como los devuelve la DGII) ----------

    private sealed record EcfReceiptWire(
        [property: JsonPropertyName("trackId")] string? TrackId,
        [property: JsonPropertyName("error")] string? Error,
        [property: JsonPropertyName("mensaje")] string? Mensaje);

    private sealed record RfceResultWire(
        [property: JsonPropertyName("codigo")] int Codigo,
        [property: JsonPropertyName("estado")] string? Estado,
        [property: JsonPropertyName("mensajes")] IReadOnlyList<MessageWire>? Mensajes,
        [property: JsonPropertyName("encf")] string? Encf,
        [property: JsonPropertyName("secuenciaUtilizada")] bool? SecuenciaUtilizada);

    private sealed record EcfResultWire(
        [property: JsonPropertyName("codigo")] int Codigo,
        [property: JsonPropertyName("estado")] string? Estado,
        [property: JsonPropertyName("mensajes")] IReadOnlyList<MessageWire>? Mensajes,
        [property: JsonPropertyName("secuenciaUtilizada")] bool? SecuenciaUtilizada,
        [property: JsonPropertyName("fechaRecepcion")] DateTimeOffset? FechaRecepcion);

    private sealed record MessageWire(
        [property: JsonPropertyName("codigo")] JsonElement Codigo,
        [property: JsonPropertyName("valor")] string? Valor)
    {
        public int CodeAsInt => Codigo.ValueKind switch
        {
            JsonValueKind.Number => Codigo.TryGetInt32(out var n) ? n : 0,
            JsonValueKind.String => int.TryParse(Codigo.GetString(), out var n) ? n : 0,
            _ => 0,
        };
    }
}
