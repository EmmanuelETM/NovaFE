using System.Linq.Expressions;
using NovaFE.Domain.Common.Entities;
using Microsoft.EntityFrameworkCore;

namespace NovaFE.Infrastructure.Persistence.EfCore;

/// <summary>
/// Contexto de EF Core. Aplica automáticamente todas las clases
/// <c>IEntityTypeConfiguration</c> de este ensamblado, así que para mapear una
/// entidad nueva solo agregas su configuración en <c>Persistence/EfCore/Configurations</c>.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    // Declara aquí un DbSet por cada agregado del dominio.

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        AplicarFiltrosDeBorradoLogico(modelBuilder);

        base.OnModelCreating(modelBuilder);
    }

    /// <summary>
    /// Toda entidad que implemente <see cref="ISoftDeletable"/> recibe un filtro
    /// global: los registros marcados como borrados no aparecen en ninguna consulta
    /// sin tener que recordar el WHERE en cada una.
    /// </summary>
    private static void AplicarFiltrosDeBorradoLogico(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes().ToList())
        {
            if (!typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
                continue;

            var parametro = Expression.Parameter(entityType.ClrType, "e");
            var cuerpo = Expression.Not(
                Expression.Property(parametro, nameof(ISoftDeletable.IsDeleted)));

            modelBuilder.Entity(entityType.ClrType)
                .HasQueryFilter(Expression.Lambda(cuerpo, parametro));
        }
    }
}
