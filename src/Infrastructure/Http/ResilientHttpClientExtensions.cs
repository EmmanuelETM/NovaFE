using Microsoft.Extensions.DependencyInjection;

namespace NovaFE.Infrastructure.Http;

public static class ResilientHttpClientExtensions
{
    /// <summary>
    /// Registra un cliente HTTP tipado con resiliencia estándar ya aplicada:
    /// reintentos con backoff exponencial, circuit breaker, timeout por intento
    /// y timeout total.
    /// <para>
    /// Los fallos resultantes se traducen con <see cref="HttpErrorMapper"/>.
    /// </para>
    /// </summary>
    /// <example>
    /// <code>
    /// services.AddResilientHttpClient&lt;IEcfGateway, EcfGateway&gt;(
    ///     new Uri(configuration["EcfGateway:BaseUrl"]!));
    /// </code>
    /// </example>
    public static IHttpClientBuilder AddResilientHttpClient<TClient, TImplementation>(
        this IServiceCollection services,
        Uri baseAddress,
        TimeSpan? timeout = null)
        where TClient : class
        where TImplementation : class, TClient
    {
        var builder = services.AddHttpClient<TClient, TImplementation>(client =>
        {
            client.BaseAddress = baseAddress;
            client.Timeout = timeout ?? TimeSpan.FromSeconds(30);
        });

        builder.AddStandardResilienceHandler();

        return builder;
    }

    /// <summary>
    /// Igual que el otro, pero la <c>BaseAddress</c> se resuelve al crear el
    /// cliente (no al registrarlo), leyendo la configuración ya combinada. Úsalo
    /// cuando la URL viene de opciones que otros orígenes (tests, variables de
    /// entorno) pueden sobreescribir.
    /// </summary>
    public static IHttpClientBuilder AddResilientHttpClient<TClient, TImplementation>(
        this IServiceCollection services,
        Func<IServiceProvider, Uri> baseAddressFactory,
        Func<IServiceProvider, TimeSpan>? timeoutFactory = null)
        where TClient : class
        where TImplementation : class, TClient
    {
        var builder = services.AddHttpClient<TClient, TImplementation>((sp, client) =>
        {
            client.BaseAddress = baseAddressFactory(sp);
            client.Timeout = timeoutFactory?.Invoke(sp) ?? TimeSpan.FromSeconds(30);
        });

        builder.AddStandardResilienceHandler();

        return builder;
    }
}
