using NovaFE.Domain.Common;

namespace NovaFE.Domain.Ecf;

/// <summary>
/// <c>&lt;CodigoModificacion&gt;</c> de la sección Información de Referencia
/// (Notas de Crédito/Débito y reemplazos).
/// </summary>
public sealed record ModificationCode(int Id, string Name) : Enumeration<ModificationCode>(Id, Name)
{
    /// <summary>1 — Anula el NCF modificado.</summary>
    public static readonly ModificationCode Voids = new(1, nameof(Voids));

    /// <summary>2 — Corrige el texto del comprobante modificado (permite <c>MontoItem = 0</c>).</summary>
    public static readonly ModificationCode CorrectsText = new(2, nameof(CorrectsText));

    /// <summary>3 — Corrige montos del NCF modificado.</summary>
    public static readonly ModificationCode CorrectsAmounts = new(3, nameof(CorrectsAmounts));

    /// <summary>4 — Reemplazo de un NCF emitido en contingencia.</summary>
    public static readonly ModificationCode ContingencyReplacement = new(4, nameof(ContingencyReplacement));

    /// <summary>5 — Referencia a una Factura de Consumo Electrónica (RFCE). Solo tipo 31.</summary>
    public static readonly ModificationCode RfceReference = new(5, nameof(RfceReference));
}

/// <summary>
/// Sección <c>&lt;InformacionReferencia&gt;</c> — obligatoria para Notas de
/// Crédito (34) y Débito (33), y en reemplazos.
/// </summary>
/// <param name="ModifiedNcf"><c>&lt;NCFModificado&gt;</c> — e-NCF o NCF de papel, ya enviado a la DGII.</param>
/// <param name="ModifiedNcfDate"><c>&lt;FechaNCFModificado&gt;</c>.</param>
/// <param name="Code"><c>&lt;CodigoModificacion&gt;</c>.</param>
/// <param name="OtherIssuerRnc"><c>&lt;RNCOtroContribuyente&gt;</c> — solo si el RNC emisor no coincide con el del NCF modificado.</param>
public sealed record EcfReference(
    string ModifiedNcf,
    DateOnly ModifiedNcfDate,
    ModificationCode Code,
    string? OtherIssuerRnc = null);
