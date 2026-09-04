namespace NovaFE.Domain.Common;

/// <summary>
/// Ambiente de la DGII contra el que opera un tenant. Determina las URLs de los
/// servicios y qué certificado se usa.
/// <list type="bullet">
/// <item><see cref="Enumeration{T}.Name"/> — clave interna nuestra (<c>Test</c> / <c>Cert</c> / <c>Production</c>).</item>
/// <item><see cref="DisplayName"/> — como lo nombra la DGII (<c>TestECF</c> / <c>CerteCF</c> / <c>eCF</c>).</item>
/// <item><see cref="UrlSegment"/> — el segmento que va en las rutas de sus servicios.</item>
/// <item><see cref="Slug"/> — forma corta en minúscula para tokens y logs (<c>test</c> / <c>cert</c> / <c>prod</c>).</item>
/// </list>
/// </summary>
public sealed record DgiiEnvironment(int Id, string Name, string DisplayName, string UrlSegment, string Slug)
    : Enumeration<DgiiEnvironment>(Id, Name)
{
    /// <summary>Pre-certificación: pruebas libres. Segmento de URL <c>testecf</c>.</summary>
    public static readonly DgiiEnvironment Test = new(1, nameof(Test), "TestECF", "testecf", "test");

    /// <summary>Certificación oficial (homologación) ante la DGII. Segmento <c>certecf</c>.</summary>
    public static readonly DgiiEnvironment Cert = new(2, nameof(Cert), "CerteCF", "certecf", "cert");

    /// <summary>Producción: emisión con validez fiscal real. Segmento <c>ecf</c>.</summary>
    public static readonly DgiiEnvironment Production = new(3, nameof(Production), "eCF", "ecf", "prod");
}
