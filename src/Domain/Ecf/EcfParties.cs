using NovaFE.Domain.Common;

namespace NovaFE.Domain.Ecf;

/// <summary>
/// Bloque <c>&lt;Emisor&gt;</c>. Sale del tenant, no del payload del cliente.
/// </summary>
/// <param name="Rnc"><c>&lt;RNCEmisor&gt;</c>.</param>
/// <param name="Name"><c>&lt;RazonSocialEmisor&gt;</c>.</param>
/// <param name="Address"><c>&lt;DireccionEmisor&gt;</c>.</param>
/// <param name="TradeName"><c>&lt;NombreComercial&gt;</c>.</param>
/// <param name="Branch"><c>&lt;Sucursal&gt;</c>.</param>
/// <param name="Municipality"><c>&lt;Municipio&gt;</c> — código Tabla III.</param>
/// <param name="Province"><c>&lt;Provincia&gt;</c> — código Tabla III.</param>
/// <param name="Phones"><c>&lt;TablaTelefonoEmisor&gt;</c> — hasta 3.</param>
/// <param name="Email"><c>&lt;CorreoEmisor&gt;</c>.</param>
/// <param name="EconomicActivity"><c>&lt;ActividadEconomica&gt;</c>.</param>
/// <param name="SellerCode"><c>&lt;CodigoVendedor&gt;</c>.</param>
/// <param name="InternalInvoiceNumber"><c>&lt;NumeroFacturaInterna&gt;</c>.</param>
/// <param name="AdditionalInfo"><c>&lt;InformacionAdicionalEmisor&gt;</c>.</param>
public sealed record EcfIssuer(
    Rnc Rnc,
    string Name,
    string Address,
    string? TradeName = null,
    string? Branch = null,
    string? Municipality = null,
    string? Province = null,
    IReadOnlyList<string>? Phones = null,
    string? Email = null,
    string? EconomicActivity = null,
    string? SellerCode = null,
    string? InternalInvoiceNumber = null,
    string? AdditionalInfo = null);

/// <summary>
/// Bloque <c>&lt;Comprador&gt;</c>. <see cref="Rnc"/> y <see cref="ForeignId"/> son
/// mutuamente excluyentes.
/// </summary>
/// <param name="Name"><c>&lt;RazonSocialComprador&gt;</c>.</param>
/// <param name="Rnc"><c>&lt;RNCComprador&gt;</c> — RNC o cédula.</param>
/// <param name="ForeignId"><c>&lt;IdentificadorExtranjero&gt;</c>.</param>
/// <param name="Contact"><c>&lt;ContactoComprador&gt;</c>.</param>
/// <param name="Email"><c>&lt;CorreoComprador&gt;</c>.</param>
/// <param name="Address"><c>&lt;DireccionComprador&gt;</c>.</param>
/// <param name="Municipality"><c>&lt;MunicipioComprador&gt;</c>.</param>
/// <param name="Province"><c>&lt;ProvinciaComprador&gt;</c>.</param>
/// <param name="AdditionalInfo"><c>&lt;InformacionAdicionalComprador&gt;</c>.</param>
public sealed record EcfBuyer(
    string Name,
    Rnc? Rnc = null,
    string? ForeignId = null,
    string? Contact = null,
    string? Email = null,
    string? Address = null,
    string? Municipality = null,
    string? Province = null,
    string? AdditionalInfo = null);
