using NovaFE.Domain.Ecf;
using NovaFE.Domain.Tenants;

namespace NovaFE.Application.Ecf.IssueEcf;

/// <summary>
/// Arma el bloque <c>&lt;Emisor&gt;</c> del e-CF combinando la identidad del
/// contribuyente (<see cref="Tenant"/>: RNC, razón social, nombre comercial), su
/// perfil fiscal (<see cref="EmitterProfile"/>: dirección, ubicación, teléfonos,
/// actividad) y los datos por-petición del payload (código de vendedor, número de
/// factura interna, información adicional).
/// </summary>
internal static class EcfIssuerFactory
{
    public static EcfIssuer From(
        Tenant tenant,
        EmitterProfile profile,
        string? sellerCode,
        string? internalInvoiceNumber,
        string? additionalInfo)
        => new(
            Rnc: tenant.Rnc,
            Name: tenant.LegalName,
            Address: profile.Address,
            TradeName: tenant.TradeName,
            Municipality: profile.Municipality,
            Province: profile.Province,
            Phones: profile.Phones is { Length: > 0 } ? profile.Phones : null,
            Email: profile.Email,
            EconomicActivity: profile.EconomicActivity,
            SellerCode: Clean(sellerCode),
            InternalInvoiceNumber: Clean(internalInvoiceNumber),
            AdditionalInfo: Clean(additionalInfo));

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
