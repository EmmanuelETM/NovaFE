using System.Linq.Expressions;
using NovaFE.Application.Common.Interfaces;
using NovaFE.Domain.Certificates;
using NovaFE.Domain.Common.Entities;
using NovaFE.Domain.Ecf;
using NovaFE.Domain.Sequences;
using NovaFE.Domain.Tenants;
using NovaFE.Infrastructure.Persistence.Audit;
using NovaFE.Infrastructure.Persistence.Idempotency;
using NovaFE.Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;

namespace NovaFE.Infrastructure.Persistence.EfCore;

/// <summary>
/// Contexto de EF Core. Aplica automáticamente todas las clases
/// <c>IEntityTypeConfiguration</c> de este ensamblado, así que para mapear una
/// entidad nueva solo agregas su configuración en <c>Persistence/EfCore/Configurations</c>.
/// </summary>
public class AppDbContext(
    DbContextOptions<AppDbContext> options,
    ICurrentTenant currentTenant) : DbContext(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<EmitterProfile> EmitterProfiles => Set<EmitterProfile>();

    /// <summary>Credenciales de acceso a la API, por contribuyente (tabla <c>api_keys</c>).</summary>
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();

    public DbSet<Certificate> Certificates => Set<Certificate>();

    public DbSet<NcfSequence> NcfSequences => Set<NcfSequence>();

    /// <summary>Comprobantes fiscales electrónicos emitidos (tabla <c>issued_ecf</c>).</summary>
    public DbSet<IssuedEcf> IssuedEcf => Set<IssuedEcf>();

    internal DbSet<IdempotencyKey> IdempotencyKeys => Set<IdempotencyKey>();

    internal DbSet<EcfSubmissionOutboxRow> EcfSubmissionOutbox => Set<EcfSubmissionOutboxRow>();

    /// <summary>Registro de auditoría inmutable (RF-14.4, tabla <c>audit_log</c>).</summary>
    internal DbSet<AuditLogRow> AuditLog => Set<AuditLogRow>();

    /// <summary>
    /// Tenant de la petición en curso. Los filtros globales de consulta de las
    /// entidades <see cref="ITenantOwned"/> lo leen; EF Core lo re-evalúa en cada
    /// consulta porque es un miembro del contexto. Es null fuera de una petición
    /// con tenant, y entonces esas entidades no devuelven ninguna fila.
    /// </summary>
    public Guid? CurrentTenantId => currentTenant.TenantId;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        ApplyGlobalQueryFilters(modelBuilder);

        base.OnModelCreating(modelBuilder);
    }

    /// <summary>
    /// Filtros globales de consulta (EF Core 10, con nombre):
    /// <list type="bullet">
    /// <item><c>SoftDelete</c> — <see cref="ISoftDeletable"/>: oculta los registros borrados.</item>
    /// <item><c>Tenant</c> — <see cref="ITenantOwned"/>: solo filas del tenant actual.
    /// Es la red de seguridad a nivel de aplicación; la base también aplica RLS.</item>
    /// </list>
    /// Para incluir registros ocultos en una consulta concreta usa
    /// <c>IgnoreQueryFilters([...])</c> con el nombre del filtro.
    /// </summary>
    private void ApplyGlobalQueryFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes().ToList())
        {
            var clrType = entityType.ClrType;

            if (typeof(ISoftDeletable).IsAssignableFrom(clrType))
            {
                var parameter = Expression.Parameter(clrType, "e");
                var body = Expression.Not(
                    Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted)));

                modelBuilder.Entity(clrType)
                    .HasQueryFilter("SoftDelete", Expression.Lambda(body, parameter));
            }

            if (typeof(ITenantOwned).IsAssignableFrom(clrType))
            {
                var filter = (LambdaExpression)GetType()
                    .GetMethod(nameof(BuildTenantFilter),
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                    .MakeGenericMethod(clrType)
                    .Invoke(this, null)!;

                modelBuilder.Entity(clrType).HasQueryFilter("Tenant", filter);
            }
        }
    }

    private Expression<Func<TEntity, bool>> BuildTenantFilter<TEntity>()
        where TEntity : class
        // e => EF.Property<Guid>(e, "TenantId") == CurrentTenantId
        // CurrentTenantId is a context member, so EF re-evaluates it per query.
        => e => EF.Property<Guid>(e, nameof(ITenantOwned.TenantId)) == CurrentTenantId;
}
