using ErrorOr;
using Microsoft.Extensions.Logging;
using NovaFE.Application.Common;
using NovaFE.Application.Common.Interfaces;
using NovaFE.Application.Ecf.Interfaces;
using NovaFE.Domain.Common;
using NovaFE.Domain.Ecf;

namespace NovaFE.Application.Ecf.Representation;

/// <param name="Layout">Formato de página; por defecto Carta.</param>
public sealed record GetEcfRepresentationQuery(Guid Id, RepresentationLayout Layout = RepresentationLayout.Letter);

/// <summary>El PDF de la Representación Impresa y el nombre de archivo sugerido.</summary>
public sealed record EcfRepresentationResult(byte[] Pdf, string FileName);

/// <summary>
/// Arma la Representación Impresa de un comprobante emitido: lee su <c>&lt;ECF&gt;</c>
/// firmado, lo proyecta al modelo de la RI (con el timbre y el estado DGII de la
/// fila) y lo renderiza a PDF. Solo lectura.
/// </summary>
public sealed class GetEcfRepresentationUseCase(
    ILoggerFactory loggerFactory,
    ICurrentTenant currentTenant,
    IEcfReadRepository ecf,
    IEcfRepresentationReader reader,
    IRepresentationRenderer renderer)
    : QueryUseCase<GetEcfRepresentationQuery, EcfRepresentationResult>(loggerFactory)
{
    protected override async Task<ErrorOr<EcfRepresentationResult>> ExecuteCore(
        GetEcfRepresentationQuery request, CancellationToken ct)
    {
        if (currentTenant.TenantId is not { } tenantId)
            return Errors.Auth.TenantNotResolved;

        if (request.Layout != RepresentationLayout.Letter)
            return Error.Validation(
                "Ecf.Representation.LayoutUnavailable",
                $"El formato de Representación Impresa '{request.Layout}' todavía no está disponible.");

        var dto = await ecf.GetByIdAsync(request.Id, tenantId, ct);
        if (dto is null)
            return EcfErrors.NotFound(request.Id);

        // La RI siempre se pinta del <ECF> completo, aunque a la DGII haya ido el RFCE.
        var xml = await ecf.GetXmlAsync(request.Id, tenantId, rfce: false, ct);
        if (xml is null)
            return EcfErrors.NotFound(request.Id);

        var verification = new RepresentationVerification(dto.SecurityCode, dto.QrUrl);
        var dgii = new RepresentationDgiiStatus(
            dto.Status, dto.Dgii?.StatusCode, dto.Dgii?.Status, dto.Dgii?.TrackId);

        var model = reader.Read(xml, verification, dgii);
        var pdf = renderer.Render(model, request.Layout);

        return new EcfRepresentationResult(pdf, $"{dto.Encf}.pdf");
    }
}
