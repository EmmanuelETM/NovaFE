namespace NovaFE.Domain.Common.Entities;

/// <summary>
/// Base para entidades con identidad. Dos entidades del mismo tipo son iguales
/// si tienen el mismo <see cref="Id"/>, independientemente de sus demás propiedades.
/// <para>
/// Ojo: dos entidades nuevas que todavía no tienen Id asignado (Id == default)
/// se consideran iguales entre sí. Compáralas por referencia hasta que se persistan.
/// </para>
/// </summary>
public abstract class Entity<TId> : IEquatable<Entity<TId>>
    where TId : notnull
{
    protected Entity(TId id) => Id = id;

    /// <summary>Constructor sin parámetros que requieren EF Core y los serializadores.</summary>
    protected Entity()
    {
    }

    public TId Id { get; protected set; } = default!;

    public bool Equals(Entity<TId>? other)
        => other is not null
           && GetType() == other.GetType()
           && EqualityComparer<TId>.Default.Equals(Id, other.Id);

    public override bool Equals(object? obj) => obj is Entity<TId> other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);

    public static bool operator ==(Entity<TId>? left, Entity<TId>? right) => Equals(left, right);

    public static bool operator !=(Entity<TId>? left, Entity<TId>? right) => !Equals(left, right);
}
