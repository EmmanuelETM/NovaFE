using NovaFE.Application.Common.Interfaces;
using NovaFE.Domain.Common.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace NovaFE.Infrastructure.Persistence.EfCore.Interceptors;

/// <summary>
/// Llena las propiedades de auditoría de toda entidad <see cref="IAuditableEntity"/>
/// en cada guardado. Nadie tiene que acordarse de asignar CreatedAt ni UpdatedBy.
/// <para>
/// Usa <see cref="TimeProvider"/> en lugar de <c>DateTimeOffset.UtcNow</c> para que
/// las pruebas puedan controlar el reloj.
/// </para>
/// </summary>
public sealed class AuditableEntityInterceptor(
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

        var ahora = timeProvider.GetUtcNow();
        var usuario = currentUser.Id;

        foreach (var entry in context.ChangeTracker.Entries<IAuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = ahora;
                    entry.Entity.CreatedBy = usuario;
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAt = ahora;
                    entry.Entity.UpdatedBy = usuario;
                    break;

                default:
                    break;
            }
        }
    }
}
