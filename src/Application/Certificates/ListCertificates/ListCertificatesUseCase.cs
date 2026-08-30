using ErrorOr;
using NovaFE.Application.Certificates.Interfaces;
using NovaFE.Application.Common;
using NovaFE.Application.Common.Interfaces;
using NovaFE.Domain.Common;
using Microsoft.Extensions.Logging;

namespace NovaFE.Application.Certificates.ListCertificates;

public sealed class ListCertificatesUseCase(
    ILoggerFactory loggerFactory,
    ICurrentTenant currentTenant,
    ICertificateReadRepository certificates)
    : ParameterlessQueryUseCase<IReadOnlyList<CertificateView>>(loggerFactory)
{
    protected override async Task<ErrorOr<IReadOnlyList<CertificateView>>> ExecuteCore(
        NoRequest request,
        CancellationToken ct)
    {
        if (!currentTenant.HasValue)
            return Errors.Auth.TenantNotResolved;

        var list = await certificates.ListAsync(ct);
        return ErrorOrFactory.From(list);
    }
}
