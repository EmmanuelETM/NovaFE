using ErrorOr;
using NovaFE.Application.Common;
using NovaFE.Application.Common.Interfaces;
using NovaFE.Application.Sequences.Contracts;
using NovaFE.Application.Sequences.Interfaces;
using NovaFE.Domain.Common;
using NovaFE.Domain.Sequences;
using Microsoft.Extensions.Logging;

namespace NovaFE.Application.Sequences.GetSequence;

public sealed class GetSequenceUseCase(
    ILoggerFactory loggerFactory,
    ICurrentTenant currentTenant,
    INcfSequenceReadRepository sequences)
    : QueryUseCase<GetSequenceQuery, NcfSequenceDto>(loggerFactory)
{
    protected override async Task<ErrorOr<NcfSequenceDto>> ExecuteCore(
        GetSequenceQuery request,
        CancellationToken ct)
    {
        if (!currentTenant.HasValue)
            return Errors.Auth.TenantNotResolved;

        var sequence = await sequences.GetByIdAsync(request.Id, ct);

        return sequence is null
            ? SequenceErrors.NotFound(request.Id)
            : sequence;
    }
}
