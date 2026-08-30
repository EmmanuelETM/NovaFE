namespace NovaFE.Domain.Common;

/// <summary>
/// Tipo de comprobante fiscal electrónico de la DGII. <see cref="Enumeration{T}.Id"/>
/// es el código de dos dígitos que va embebido en el e-NCF (31, 32, …);
/// <see cref="Enumeration{T}.Name"/> es la clave interna en inglés;
/// <see cref="DisplayName"/> es como lo nombra la DGII de cara al contribuyente.
/// <para>
/// <see cref="HasSequenceExpiry"/> distingue los tipos cuya secuencia vence el 31
/// de diciembre del año siguiente a la autorización de los que no llevan
/// <c>FechaVencimientoSecuencia</c> (tipos 32 y 34, obligatoriedad 0).
/// </para>
/// </summary>
public sealed record EcfType(int Id, string Name, string DisplayName, bool HasSequenceExpiry)
    : Enumeration<EcfType>(Id, Name)
{
    /// <summary>31 — Factura de Crédito Fiscal Electrónica.</summary>
    public static readonly EcfType CreditoFiscal =
        new(31, nameof(CreditoFiscal), "Factura de Crédito Fiscal Electrónica", HasSequenceExpiry: true);

    /// <summary>32 — Factura de Consumo Electrónica. Sin vencimiento de secuencia.</summary>
    public static readonly EcfType Consumo =
        new(32, nameof(Consumo), "Factura de Consumo Electrónica", HasSequenceExpiry: false);

    /// <summary>33 — Nota de Débito Electrónica.</summary>
    public static readonly EcfType NotaDebito =
        new(33, nameof(NotaDebito), "Nota de Débito Electrónica", HasSequenceExpiry: true);

    /// <summary>34 — Nota de Crédito Electrónica. Sin vencimiento de secuencia.</summary>
    public static readonly EcfType NotaCredito =
        new(34, nameof(NotaCredito), "Nota de Crédito Electrónica", HasSequenceExpiry: false);

    /// <summary>41 — Comprobante Electrónico de Compras.</summary>
    public static readonly EcfType Compras =
        new(41, nameof(Compras), "Comprobante Electrónico de Compras", HasSequenceExpiry: true);

    /// <summary>43 — Comprobante Electrónico para Gastos Menores.</summary>
    public static readonly EcfType GastosMenores =
        new(43, nameof(GastosMenores), "Comprobante Electrónico para Gastos Menores", HasSequenceExpiry: true);

    /// <summary>44 — Comprobante Electrónico para Regímenes Especiales.</summary>
    public static readonly EcfType RegimenesEspeciales =
        new(44, nameof(RegimenesEspeciales), "Comprobante Electrónico para Regímenes Especiales", HasSequenceExpiry: true);

    /// <summary>45 — Comprobante Electrónico Gubernamental.</summary>
    public static readonly EcfType Gubernamental =
        new(45, nameof(Gubernamental), "Comprobante Electrónico Gubernamental", HasSequenceExpiry: true);

    /// <summary>46 — Comprobante Electrónico para Exportaciones.</summary>
    public static readonly EcfType Exportaciones =
        new(46, nameof(Exportaciones), "Comprobante Electrónico para Exportaciones", HasSequenceExpiry: true);

    /// <summary>47 — Comprobante Electrónico para Pagos al Exterior.</summary>
    public static readonly EcfType PagosExterior =
        new(47, nameof(PagosExterior), "Comprobante Electrónico para Pagos al Exterior", HasSequenceExpiry: true);

    /// <summary>El tipo cuyo código es <paramref name="code"/>, o null si no es uno de los diez.</summary>
    public static EcfType? FromCodeOrDefault(int code) =>
        GetAll().FirstOrDefault(type => type.Id == code);
}
