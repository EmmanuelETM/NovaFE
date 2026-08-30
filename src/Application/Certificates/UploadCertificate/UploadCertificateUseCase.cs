using ErrorOr;
using FluentValidation;
using NovaFE.Application.Certificates.Interfaces;
using NovaFE.Application.Common;
using NovaFE.Application.Common.Interfaces;
using NovaFE.Application.Tenants.Interfaces;
using NovaFE.Domain.Certificates;
using NovaFE.Domain.Common;
using NovaFE.Domain.Tenants;
using Microsoft.Extensions.Logging;

namespace NovaFE.Application.Certificates.UploadCertificate;

public sealed class UploadCertificateUseCase(
    ILoggerFactory loggerFactory,
    IValidator<UploadCertificateCommand> validator,
    TimeProvider timeProvider,
    ICurrentTenant currentTenant,
    ITenantRepository tenants,
    ICertificateRepository certificates,
    ICertificateVault vault)
    : CommandUseCase<UploadCertificateCommand, Guid>(loggerFactory, validator)
{
    protected override async Task<ErrorOr<Guid>> ExecuteCore(
        UploadCertificateCommand request,
        CancellationToken ct)
    {
        if (currentTenant.TenantId is not { } tenantId)
            return Errors.Auth.TenantNotResolved;

        var environment = DgiiEnvironment.GetAll()
            .First(e => string.Equals(e.Name, request.Environment.Trim(), StringComparison.OrdinalIgnoreCase));

        var tenant = await tenants.GetByIdAsync(tenantId, ct);
        if (tenant is null)
            return TenantErrors.NotFound(tenantId);

        var inspection = CertificateInspector.Inspect(request.Content, request.Password);
        if (inspection.IsError)
            return inspection.Errors;

        if (await certificates.HasActiveCertificateAsync(environment, ct))
            return CertificateErrors.EnvironmentHasActiveCertificate(environment.DisplayName);

        var reference = await vault.StoreAsync(request.Content, request.Password, ct);

        var certificate = Certificate.Issue(
            tenant.Rnc,
            environment,
            inspection.Value,
            reference,
            timeProvider.GetUtcNow());

        if (certificate.IsError)
        {
            await vault.DeleteAsync(reference, ct);
            return certificate.Errors;
        }

        await certificates.AddAsync(certificate.Value, ct);

        return certificate.Value.Id;
    }
}
