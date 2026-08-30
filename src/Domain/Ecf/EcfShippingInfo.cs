namespace NovaFE.Domain.Ecf;

/// <summary>
/// <c>&lt;InformacionesAdicionales&gt;</c> del encabezado — datos de embarque y
/// logística. Bloque **opcional** (obligatoriedad 3) en los tipos 31/32/33/34/44/45/46;
/// no aplica a 41/43/47. Todos los campos los trae el cliente; el motor fiscal no
/// los usa.
/// </summary>
/// <param name="ShipmentDate"><c>&lt;FechaEmbarque&gt;</c>.</param>
/// <param name="ShipmentNumber"><c>&lt;NumeroEmbarque&gt;</c>.</param>
/// <param name="ContainerNumber"><c>&lt;NumeroContenedor&gt;</c>.</param>
/// <param name="ReferenceNumber"><c>&lt;NumeroReferencia&gt;</c>.</param>
/// <param name="GrossWeight"><c>&lt;PesoBruto&gt;</c>.</param>
/// <param name="NetWeight"><c>&lt;PesoNeto&gt;</c>.</param>
/// <param name="GrossWeightUnit"><c>&lt;UnidadPesoBruto&gt;</c> — código Tabla IV.</param>
/// <param name="NetWeightUnit"><c>&lt;UnidadPesoNeto&gt;</c> — código Tabla IV.</param>
/// <param name="PackageCount"><c>&lt;CantidadBulto&gt;</c>.</param>
/// <param name="PackageUnit"><c>&lt;UnidadBulto&gt;</c> — código Tabla IV.</param>
/// <param name="Volume"><c>&lt;VolumenBulto&gt;</c>.</param>
/// <param name="VolumeUnit"><c>&lt;UnidadVolumen&gt;</c> — código Tabla IV.</param>
/// <param name="Export">Datos de exportación (FOB/CIF/puertos) — solo tipo 46.</param>
public sealed record EcfShippingInfo(
    DateOnly? ShipmentDate = null,
    string? ShipmentNumber = null,
    string? ContainerNumber = null,
    string? ReferenceNumber = null,
    decimal? GrossWeight = null,
    decimal? NetWeight = null,
    string? GrossWeightUnit = null,
    string? NetWeightUnit = null,
    decimal? PackageCount = null,
    string? PackageUnit = null,
    decimal? Volume = null,
    string? VolumeUnit = null,
    EcfExportDetails? Export = null);

/// <summary>
/// Campos de exportación de <c>&lt;InformacionesAdicionales&gt;</c> — <b>solo tipo 46</b>
/// (Exportaciones). El XSD los intercala entre <c>NumeroReferencia</c> y
/// <c>PesoBruto</c>.
/// </summary>
/// <param name="LoadingPortName"><c>&lt;NombrePuertoEmbarque&gt;</c>.</param>
/// <param name="DeliveryTerms"><c>&lt;CondicionesEntrega&gt;</c> — Incoterm de 3 letras (FOB, CIF…).</param>
/// <param name="TotalFob"><c>&lt;TotalFob&gt;</c>.</param>
/// <param name="Insurance"><c>&lt;Seguro&gt;</c>.</param>
/// <param name="Freight"><c>&lt;Flete&gt;</c>.</param>
/// <param name="OtherCharges"><c>&lt;OtrosGastos&gt;</c>.</param>
/// <param name="TotalCif"><c>&lt;TotalCif&gt;</c> — ≈ FOB + Seguro + Flete + OtrosGastos.</param>
/// <param name="CustomsRegime"><c>&lt;RegimenAduanero&gt;</c>.</param>
/// <param name="DeparturePortName"><c>&lt;NombrePuertoSalida&gt;</c>.</param>
/// <param name="UnloadingPortName"><c>&lt;NombrePuertoDesembarque&gt;</c>.</param>
public sealed record EcfExportDetails(
    string? LoadingPortName = null,
    string? DeliveryTerms = null,
    decimal? TotalFob = null,
    decimal? Insurance = null,
    decimal? Freight = null,
    decimal? OtherCharges = null,
    decimal? TotalCif = null,
    string? CustomsRegime = null,
    string? DeparturePortName = null,
    string? UnloadingPortName = null);
