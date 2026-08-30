using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using NovaFE.Application.Dgii;
using NovaFE.Domain.Common;
using NovaFE.Domain.Common.Json;
using NovaFE.Domain.Dgii;
using NovaFE.Infrastructure.Http;
using ErrorOr;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace NovaFE.Infrastructure.Dgii;

/// <summary>
/// Cliente HTTP del servicio de autenticación de la DGII. El <see cref="HttpClient"/>
/// viene con resiliencia estándar (reintentos, circuit breaker, timeouts) por
/// <c>AddResilientHttpClient</c>; su <c>BaseAddress</c> es el dominio de e-CF y
/// aquí solo se arma el resto de la ruta.
/// </summary>
internal sealed class DgiiAuthClient(HttpClient http, TimeProvider timeProvider) : IDgiiAuthClient
{
    public async Task<ErrorOr<string>> GetSeedAsync(DgiiEnvironment environment, CancellationToken ct = default)
    {
        try
        {
            using var response = await http.GetAsync(
                $"{environment.UrlSegment}/autenticacion/api/autenticacion/semilla", ct);

            if (!response.IsSuccessStatusCode)
                return DgiiAuthErrors.SeedRequestFailed((int)response.StatusCode);

            var xml = await response.Content.ReadAsStringAsync(ct);

            return string.IsNullOrWhiteSpace(xml)
                ? DgiiAuthErrors.SeedRequestFailed((int)response.StatusCode)
                : xml;
        }
        catch (Exception ex) when (IsTransportFailure(ex))
        {
            return HttpErrorMapper.Map(ex);
        }
    }

    public async Task<ErrorOr<AuthenticationToken>> ValidateSeedAsync(
        DgiiEnvironment environment,
        string signedSeedXml,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(signedSeedXml);

        try
        {
            using var form = new MultipartFormDataContent();
            var xmlPart = new StringContent(signedSeedXml, new UTF8Encoding(false), "application/xml");
            form.Add(xmlPart, name: "xml", fileName: "semilla.xml");

            using var response = await http.PostAsync(
                $"{environment.UrlSegment}/autenticacion/api/autenticacion/validarsemilla", form, ct);

            if (!response.IsSuccessStatusCode)
                return DgiiAuthErrors.TokenRequestFailed((int)response.StatusCode);

            TokenPayload? payload;
            try
            {
                payload = await response.Content.ReadFromJsonAsync<TokenPayload>(JsonSettings.Bulletproof, ct);
            }
            catch (JsonException)
            {
                return DgiiAuthErrors.MalformedTokenResponse;
            }

            if (payload is null || string.IsNullOrWhiteSpace(payload.Token))
                return DgiiAuthErrors.TokenRejected("la respuesta no trae un token");

            var issuedAt = payload.Expedido == default ? timeProvider.GetUtcNow() : payload.Expedido;

            if (payload.Expira <= issuedAt)
                return DgiiAuthErrors.MalformedTokenResponse;

            return new AuthenticationToken(payload.Token, issuedAt, payload.Expira);
        }
        catch (Exception ex) when (IsTransportFailure(ex))
        {
            return HttpErrorMapper.Map(ex);
        }
    }

    private static bool IsTransportFailure(Exception ex) => ex
        is HttpRequestException
        or TaskCanceledException
        or TimeoutRejectedException
        or BrokenCircuitException;

    private sealed record TokenPayload(string Token, DateTimeOffset Expira, DateTimeOffset Expedido);
}
