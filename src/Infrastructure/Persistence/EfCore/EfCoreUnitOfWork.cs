using NovaFE.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace NovaFE.Infrastructure.Persistence.EfCore;

/// <summary>
/// Unidad de trabajo sobre EF Core.
/// <para>
/// Usa la estrategia de ejecución del proveedor, que es obligatorio cuando los
/// reintentos ante fallos transitorios están activados: sin esto, abrir una
/// transacción propia con <c>EnableRetryOnFailure</c> lanza una excepción en
/// tiempo de ejecución.
/// </para>
/// </summary>
internal sealed class EfCoreUnitOfWork(AppDbContext context) : IUnitOfWork
{
    public async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        var strategy = context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await context.Database.BeginTransactionAsync(ct);

            try
            {
                var result = await operation(ct);

                // Red de seguridad: persiste cualquier cambio que quedara rastreado
                // sin guardar. Los repositorios ya guardan por su cuenta.
                await context.SaveChangesAsync(ct);

                await transaction.CommitAsync(ct);

                return result;
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        });
    }

    public async Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        await ExecuteInTransactionAsync<object?>(async token =>
        {
            await operation(token);
            return null;
        }, ct);
    }
}
