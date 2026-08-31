using ErrorOr;
using NovaFE.Application.Common;
using NovaFE.Application.Common.Interfaces;
using NovaFE.Application.Ecf.Contracts;
using NovaFE.Application.Ecf.Interfaces;
using NovaFE.Domain.Common;
using NovaFE.Domain.Ecf;
using Microsoft.Extensions.Logging;

namespace NovaFE.Application.Ecf.RetrySubmission;

/// <summary>Reencola el envío de un comprobante en estado <c>failed</c> o <c>review</c>.</summary>
public sealed record RetryEcfSubmissionCommand(Guid Id);

public sealed class RetryEcfSubmissionUseCase(
    ILoggerFactory loggerFactory,
    ICurrentTenant currentTenant,
    IEcfRepository ecf,
    IEcfReadRepository ecfReads,
    IEcfSubmissionQueue queue,
    IUnitOfWork unitOfWork)
    : CommandUseCase<RetryEcfSubmissionCommand, EcfDto>(loggerFactory)
{
    protected override async Task<ErrorOr<EcfDto>> ExecuteCore(RetryEcfSubmissionCommand request, CancellationToken ct)
    {
        if (currentTenant.TenantId is not { } tenantId)
            return Errors.Auth.TenantNotResolved;

        var issued = await ecf.GetByIdAsync(request.Id, ct);
        if (issued is null)
            return EcfErrors.NotFound(request.Id);

        var requeued = issued.RequeueForRetry();
        if (requeued.IsError)
            return requeued.Errors;

        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            await ecf.UpdateAsync(issued, token);
            await queue.EnqueueSubmitAsync(issued.Id, tenantId, issued.Environment, token);
        }, ct);

        var dto = await ecfReads.GetByIdAsync(request.Id, tenantId, ct);
        return dto is null ? EcfErrors.NotFound(request.Id) : dto;
    }
}
