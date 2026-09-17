using ErrorOr;
using NovaFE.Application.Common;
using NovaFE.Application.Common.Interfaces;
using NovaFE.Application.Sequences.Interfaces;
using NovaFE.Domain.Common;
using NovaFE.Domain.Sequences;
using Microsoft.Extensions.Logging;

namespace NovaFE.Application.Sequences.DeactivateSequenceRange;

public sealed class DeactivateSequenceRangeUseCase(
    ILoggerFactory loggerFactory,
    ICurrentTenant currentTenant,
    INcfSequenceRepository sequences)
    : CommandUseCase<DeactivateSequenceRangeCommand>(loggerFactory)
{
    protected override async Task<ErrorOr<Success>> ExecuteCore(
        DeactivateSequenceRangeCommand request,
        CancellationToken ct)
    {
        if (!currentTenant.HasValue)
            return Errors.Auth.TenantNotResolved;

        var sequence = await sequences.GetByIdAsync(request.Id, ct);
        if (sequence is null)
            return SequenceErrors.NotFound(request.Id);

        var deactivation = sequence.Deactivate();
        if (deactivation.IsError)
            return deactivation.Errors;

        await sequences.UpdateAsync(sequence, ct);

        return Result.Success;
    }
}
