using NovaFE.Application.Common.Interfaces;
using NovaFE.Domain.Common.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace NovaFE.Infrastructure.Persistence.EfCore.Interceptors;

/// <summary>
/// Convierte el borrado físico en borrado lógico para toda entidad
/// <see cref="ISoftDeletable"/>: un <c>Remove()</c> normal se traduce a un UPDATE
/// que marca el registro, y el filtro global del contexto lo saca de las consultas.
/// </summary>
public sealed class SoftDeleteInterceptor(
    ICurrentUser currentUser,
    TimeProvider timeProvider) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        Aplicar(eventData.Context);

        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        Aplicar(eventData.Context);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Aplicar(DbContext? context)
    {
        if (context is null)
            return;

        foreach (var entry in context.ChangeTracker.Entries<ISoftDeletable>())
        {
            if (entry.State != EntityState.Deleted)
                continue;

            entry.State = EntityState.Modified;

            entry.Entity.IsDeleted = true;
            entry.Entity.DeletedAt = timeProvider.GetUtcNow();
            entry.Entity.DeletedBy = currentUser.Id;
        }
    }
}
