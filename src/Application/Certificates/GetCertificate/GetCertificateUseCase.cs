using ErrorOr;
using NovaFE.Application.Certificates.Interfaces;
using NovaFE.Application.Common;
using NovaFE.Application.Common.Interfaces;
using NovaFE.Domain.Certificates;
using NovaFE.Domain.Common;
using Microsoft.Extensions.Logging;

namespace NovaFE.Application.Certificates.GetCertificate;

public sealed class GetCertificateUseCase(
    ILoggerFactory loggerFactory,
    ICurrentTenant currentTenant,
    ICertificateReadRepository certificates)
    : QueryUseCase<GetCertificateQuery, CertificateView>(loggerFactory)
{
    protected override async Task<ErrorOr<CertificateView>> ExecuteCore(
        GetCertificateQuery request,
        CancellationToken ct)
    {
        if (!currentTenant.HasValue)
            return Errors.Auth.TenantNotResolved;

        var certificate = await certificates.GetByIdAsync(request.Id, ct);

        return certificate is null
            ? CertificateErrors.NotFound(request.Id)
            : certificate;
    }
}
