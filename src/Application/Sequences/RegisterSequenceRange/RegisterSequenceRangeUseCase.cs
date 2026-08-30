using ErrorOr;
using FluentValidation;
using NovaFE.Application.Common;
using NovaFE.Application.Common.Interfaces;
using NovaFE.Application.Sequences.Interfaces;
using NovaFE.Domain.Common;
using NovaFE.Domain.Sequences;
using Microsoft.Extensions.Logging;

namespace NovaFE.Application.Sequences.RegisterSequenceRange;

public sealed class RegisterSequenceRangeUseCase(
    ILoggerFactory loggerFactory,
    IValidator<RegisterSequenceRangeCommand> validator,
    TimeProvider timeProvider,
    ICurrentTenant currentTenant,
    INcfSequenceRepository sequences)
    : CommandUseCase<RegisterSequenceRangeCommand, Guid>(loggerFactory, validator)
{
    protected override async Task<ErrorOr<Guid>> ExecuteCore(
        RegisterSequenceRangeCommand request,
        CancellationToken ct)
    {
        if (!currentTenant.HasValue)
            return Errors.Auth.TenantNotResolved;

        var type = EcfType.FromCodeOrDefault(request.Type);
        if (type is null)
            return SequenceErrors.UnknownType(request.Type);

        var environment = DgiiEnvironment.GetAll()
            .First(e => string.Equals(e.Name, request.Environment.Trim(), StringComparison.OrdinalIgnoreCase));

        var series = char.ToUpperInvariant(request.Series.Trim()[0]);

        var today = timeProvider.GetDominicanToday();
        var authorizedOn = request.AuthorizedOn ?? today;
        if (authorizedOn > today)
            return SequenceErrors.AuthorizationInTheFuture;

        if (await sequences.HasActiveRangeAsync(environment, type, series, ct))
            return SequenceErrors.SeriesAlreadyActive(series, type.DisplayName);

        var sequence = NcfSequence.Authorize(
            environment, type, series, request.RangeFrom, request.RangeTo, authorizedOn);
        if (sequence.IsError)
            return sequence.Errors;

        await sequences.AddAsync(sequence.Value, ct);

        return sequence.Value.Id;
    }
}
