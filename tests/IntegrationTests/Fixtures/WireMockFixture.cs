using WireMock.Server;

namespace NovaFE.IntegrationTests.Fixtures;

/// <summary>
/// Servidor HTTP falso para simular servicios externos (por ejemplo ECFGateway).
/// <para>
/// Es lo que permite probar de verdad los caminos difíciles de un cliente HTTP
/// resiliente: un 500, un timeout, o la apertura del circuito. Contra el servicio
/// real esos escenarios no se pueden provocar a voluntad.
/// </para>
/// </summary>
/// <example>
/// <code>
/// using var externo = new WireMockFixture();
/// externo.Server
///     .Given(Request.Create().WithPath("/comprobantes/1").UsingGet())
///     .RespondWith(Response.Create().WithStatusCode(500));
///
/// // Apunta el cliente al servidor falso:
/// // ["EcfGateway:BaseUrl"] = externo.BaseUrl
/// </code>
/// </example>
public sealed class WireMockFixture : IDisposable
{
    public WireMockFixture() => Server = WireMockServer.Start();

    public WireMockServer Server { get; }

    /// <summary>URL base a la que hay que apuntar el cliente bajo prueba.</summary>
    public string BaseUrl => Server.Url!;

    public void Dispose()
    {
        Server.Stop();
        Server.Dispose();
    }
}
