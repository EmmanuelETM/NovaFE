using NovaFE.Application.Common.Interfaces;
using NovaFE.Domain.Common.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace NovaFE.Infrastructure.Persistence.EfCore.Interceptors;

/// <summary>
/// Estampa <c>TenantId</c> en cada entidad <see cref="ITenantOwned"/> nueva a
/// partir del tenant de la petición en curso, y rechaza cualquier escritura que
/// intente tocar datos de otro tenant. Es la contraparte de escritura del filtro
/// global de consulta del contexto.
/// </summary>
public sealed class TenantStampingInterceptor(ICurrentTenant currentTenant) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        Apply(eventData.Context);

        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        Apply(eventData.Context);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Apply(DbContext? context)
    {
        if (context is null)
            return;

        foreach (var entry in context.ChangeTracker.Entries<ITenantOwned>())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified))
                continue;

            var property = entry.Property(nameof(ITenantOwned.TenantId));
            var currentValue = (Guid)(property.CurrentValue ?? Guid.Empty);

            if (entry.State == EntityState.Added && currentValue == Guid.Empty)
            {
                property.CurrentValue = currentTenant.Require();
                continue;
            }

            // Ya trae TenantId (recarga, o asignado a mano): tiene que ser el de
            // la petición. Nunca se escriben datos de otro tenant.
            if (currentTenant.HasValue && currentValue != currentTenant.TenantId)
            {
                throw new InvalidOperationException(
                    $"Se intentó escribir una entidad '{entry.Metadata.ClrType.Name}' del tenant " +
                    $"'{currentValue}' durante una petición del tenant '{currentTenant.TenantId}'.");
            }
        }
    }
}
