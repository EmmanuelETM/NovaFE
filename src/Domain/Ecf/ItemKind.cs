using NovaFE.Domain.Common;

namespace NovaFE.Domain.Ecf;

/// <summary>
/// <c>&lt;IndicadorBienoServicio&gt;</c> del detalle: <b>1 = Bien, 2 = Servicio</b>
/// (verificado contra <c>e-CF 31 v.1.0.xsd</c> — el contexto viejo decía B/S).
/// </summary>
public sealed record ItemKind(int Id, string Name) : Enumeration<ItemKind>(Id, Name)
{
    public static readonly ItemKind Good = new(1, nameof(Good));

    public static readonly ItemKind Service = new(2, nameof(Service));
}
