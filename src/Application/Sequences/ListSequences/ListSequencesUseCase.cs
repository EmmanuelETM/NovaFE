using ErrorOr;
using NovaFE.Application.Common;
using NovaFE.Application.Common.Interfaces;
using NovaFE.Application.Sequences.Interfaces;
using NovaFE.Application.Sequences.ReadModels;
using NovaFE.Domain.Common;
using Microsoft.Extensions.Logging;

namespace NovaFE.Application.Sequences.ListSequences;

public sealed class ListSequencesUseCase(
    ILoggerFactory loggerFactory,
    ICurrentTenant currentTenant,
    INcfSequenceReadRepository sequences)
    : ParameterlessQueryUseCase<IReadOnlyList<NcfSequenceView>>(loggerFactory)
{
    protected override async Task<ErrorOr<IReadOnlyList<NcfSequenceView>>> ExecuteCore(
        NoRequest request,
        CancellationToken ct)
    {
        if (!currentTenant.HasValue)
            return Errors.Auth.TenantNotResolved;

        var list = await sequences.ListAsync(ct);
        return ErrorOrFactory.From(list);
    }
}
