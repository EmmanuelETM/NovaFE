using ErrorOr;
using NovaFE.Application.Common.Interfaces;
using NovaFE.Application.Sequences.Interfaces;
using NovaFE.Domain.Common;
using NovaFE.Domain.Sequences;
using Microsoft.EntityFrameworkCore;

namespace NovaFE.Infrastructure.Persistence.EfCore.Repositories;

/// <summary>
/// Asignación atómica de secuencias e-NCF (RF-07.2).
/// <para>
/// Abre una transacción sobre la estrategia de ejecución del proveedor (obligatorio
/// con los reintentos activados) y bloquea con <c>SELECT … FOR UPDATE</c> todos los
/// rangos activos del tipo. Bajo concurrencia, la segunda petición espera a que la
/// primera haga commit antes de leer el puntero, así que nunca comparten número.
/// </para>
/// <para>
/// El SQL crudo trae el <c>FOR UPDATE</c> y filtra el tenant de forma explícita;
/// por eso ignora los filtros globales de consulta (que, de aplicarse, envolverían
/// la consulta en una subconsulta y anularían el lock).
/// </para>
/// </summary>
internal sealed class NcfSequenceAllocator(
    AppDbContext context,
    ICurrentTenant currentTenant,
    TimeProvider timeProvider) : INcfSequenceAllocator
{
    public async Task<ErrorOr<Encf>> AllocateAsync(
        DgiiEnvironment environment,
        EcfType type,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(type);

        if (currentTenant.TenantId is not { } tenantId)
            return Errors.Auth.TenantNotResolved;

        var today = timeProvider.GetDominicanToday();
        var typeCode = (short)type.Id;

        var strategy = context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await context.Database.BeginTransactionAsync(ct);

            try
            {
                var ranges = await context.NcfSequences
                    .FromSql(
                        $"""
                        SELECT * FROM ncf_sequences
                        WHERE tenant_id = {tenantId}
                          AND environment = {environment.Name}
                          AND ecf_type = {typeCode}
                          AND active = true
                          AND is_deleted = false
                        ORDER BY series, range_from
                        FOR UPDATE
                        """)
                    .IgnoreQueryFilters()
                    .ToListAsync(ct);

                var result = TryAllocate(ranges, type, today);

                if (!result.IsError)
                {
                    await context.SaveChangesAsync(ct);
                    await transaction.CommitAsync(ct);
                }
                else
                {
                    await transaction.RollbackAsync(ct);
                }

                return result;
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        });
    }

    private static ErrorOr<Encf> TryAllocate(List<NcfSequence> ranges, EcfType type, DateOnly today)
    {
        if (ranges.Count == 0)
            return SequenceErrors.NoAuthorizedRange(type.DisplayName);

        var live = ranges.Where(range => !range.IsExpired(today)).ToList();
        if (live.Count == 0)
            return SequenceErrors.AllRangesExpired(type.DisplayName);

        foreach (var range in live)
        {
            var allocation = range.Allocate(today);
            if (!allocation.IsError)
                return allocation;
        }

        return SequenceErrors.AllRangesExhausted(type.DisplayName);
    }
}
