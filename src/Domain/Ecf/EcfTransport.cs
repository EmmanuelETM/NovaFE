using NovaFE.Domain.Common;

namespace NovaFE.Domain.Ecf;

/// <summary>
/// <c>&lt;ViaTransporte&gt;</c> — solo tipo 46 (Exportaciones). Verificado contra
/// <c>e-CF 46 v.1.0.xsd</c>, <c>ViaTransporteType</c>.
/// </summary>
public sealed record TransportVia(int Id, string Name, string Code) : Enumeration<TransportVia>(Id, Name)
{
    /// <summary>01 — Terrestre.</summary>
    public static readonly TransportVia Land = new(1, nameof(Land), "01");

    /// <summary>02 — Marítimo.</summary>
    public static readonly TransportVia Sea = new(2, nameof(Sea), "02");

    /// <summary>03 — Aérea.</summary>
    public static readonly TransportVia Air = new(3, nameof(Air), "03");
}

/// <summary>
/// <c>&lt;Transporte&gt;</c> del encabezado. Bloque **opcional** (obligatoriedad 3)
/// en 31/32/33/34/44/45/46/47; no aplica a 41/43. El tipo 47 solo admite
/// <see cref="DestinationCountry"/>; el tipo 46 agrega los campos de vía/país/
/// compañía transportista. El resto de tipos solo lleva los campos básicos
/// (<see cref="Driver"/>…<see cref="DeliveryNote"/>). Passthrough — el motor no lo usa.
/// </summary>
/// <param name="Driver"><c>&lt;Conductor&gt;</c>.</param>
/// <param name="TransportDocument"><c>&lt;DocumentoTransporte&gt;</c>.</param>
/// <param name="VehicleId"><c>&lt;Ficha&gt;</c>.</param>
/// <param name="Plate"><c>&lt;Placa&gt;</c>.</param>
/// <param name="Route"><c>&lt;RutaTransporte&gt;</c>.</param>
/// <param name="Zone"><c>&lt;ZonaTransporte&gt;</c>.</param>
/// <param name="DeliveryNote"><c>&lt;NumeroAlbaran&gt;</c>.</param>
/// <param name="Via"><c>&lt;ViaTransporte&gt;</c> — solo tipo 46.</param>
/// <param name="OriginCountry"><c>&lt;PaisOrigen&gt;</c> — solo tipo 46.</param>
/// <param name="DestinationAddress"><c>&lt;DireccionDestino&gt;</c> — solo tipo 46.</param>
/// <param name="DestinationCountry"><c>&lt;PaisDestino&gt;</c> — tipos 46 y 47.</param>
/// <param name="CarrierRnc"><c>&lt;RNCIdentificacionCompaniaTransportista&gt;</c> — solo tipo 46.</param>
/// <param name="CarrierName"><c>&lt;NombreCompaniaTransportista&gt;</c> — solo tipo 46.</param>
/// <param name="VoyageNumber"><c>&lt;NumeroViaje&gt;</c> — solo tipo 46.</param>
public sealed record EcfTransport(
    string? Driver = null,
    string? TransportDocument = null,
    string? VehicleId = null,
    string? Plate = null,
    string? Route = null,
    string? Zone = null,
    string? DeliveryNote = null,
    TransportVia? Via = null,
    string? OriginCountry = null,
    string? DestinationAddress = null,
    string? DestinationCountry = null,
    string? CarrierRnc = null,
    string? CarrierName = null,
    string? VoyageNumber = null);
