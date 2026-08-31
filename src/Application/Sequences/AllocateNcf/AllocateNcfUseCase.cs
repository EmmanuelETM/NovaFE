using ErrorOr;
using FluentValidation;
using NovaFE.Application.Common;
using NovaFE.Application.Common.Interfaces;
using NovaFE.Application.Sequences.Contracts;
using NovaFE.Application.Sequences.Interfaces;
using NovaFE.Domain.Common;
using NovaFE.Domain.Sequences;
using Microsoft.Extensions.Logging;

namespace NovaFE.Application.Sequences.AllocateNcf;

public sealed class AllocateNcfUseCase(
    ILoggerFactory loggerFactory,
    IValidator<AllocateNcfCommand> validator,
    ICurrentTenant currentTenant,
    INcfSequenceAllocator allocator)
    : CommandUseCase<AllocateNcfCommand, AllocatedNcfDto>(loggerFactory, validator)
{
    protected override async Task<ErrorOr<AllocatedNcfDto>> ExecuteCore(
        AllocateNcfCommand request,
        CancellationToken ct)
    {
        if (!currentTenant.HasValue)
            return Errors.Auth.TenantNotResolved;

        var type = EcfType.FromCodeOrDefault(request.Type);
        if (type is null)
            return SequenceErrors.UnknownType(request.Type);

        var environment = DgiiEnvironment.GetAll()
            .First(e => string.Equals(e.Name, request.Environment.Trim(), StringComparison.OrdinalIgnoreCase));

        var allocation = await allocator.AllocateAsync(environment, type, ct);
        if (allocation.IsError)
            return allocation.Errors;

        var encf = allocation.Value.Encf;
        return new AllocatedNcfDto(encf.Value, encf.TypeCode, encf.Series.ToString(), encf.Sequential);
    }
}
