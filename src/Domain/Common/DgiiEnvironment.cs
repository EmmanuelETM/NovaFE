namespace NovaFE.Domain.Common;

/// <summary>
/// Ambiente de la DGII contra el que opera un tenant. Determina las URLs de los
/// servicios y qué certificado se usa. <see cref="Enumeration{T}.Name"/> es la
/// clave interna; <see cref="DisplayName"/> es como lo nombra la DGII.
/// </summary>
public sealed record DgiiEnvironment(int Id, string Name, string DisplayName)
    : Enumeration<DgiiEnvironment>(Id, Name)
{
    /// <summary>Pre-certificación: pruebas libres. Segmento de URL <c>/testecf/</c>.</summary>
    public static readonly DgiiEnvironment TestEcf = new(1, nameof(TestEcf), "TestECF");

    /// <summary>Certificación oficial (homologación) ante la DGII. Segmento <c>/certecf/</c>.</summary>
    public static readonly DgiiEnvironment CertEcf = new(2, nameof(CertEcf), "CerteCF");

    /// <summary>Producción: emisión con validez fiscal real. Segmento <c>/ecf/</c>.</summary>
    public static readonly DgiiEnvironment Production = new(3, nameof(Production), "eCF");
}
