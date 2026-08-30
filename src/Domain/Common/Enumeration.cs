using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace NovaFE.Domain.Common;

/// <summary>
/// Alternativa a <c>enum</c> para valores de dominio que necesitan comportamiento,
/// descripción o datos asociados. Se declaran como campos estáticos en la clase hija.
/// </summary>
/// <example>
/// <code>
/// public sealed record EstadoSolicitud(int Id, string Name) : Enumeration&lt;EstadoSolicitud&gt;(Id, Name)
/// {
///     public static readonly EstadoSolicitud Pendiente = new(1, "Pendiente");
///     public static readonly EstadoSolicitud Aprobada  = new(2, "Aprobada");
/// }
/// </code>
/// </example>
[SuppressMessage("Design", "CA1000:Do not declare static members on generic types",
    Justification = "El patrón smart enum requiere fábricas estáticas tipadas (GetAll/FromValue/FromName) sobre el tipo genérico.")]
public abstract record Enumeration<T>(int Id, string Name) : IComparable<T>
    where T : Enumeration<T>
{
    /// <summary>Todas las instancias declaradas como campos estáticos en la clase hija.</summary>
    public static IEnumerable<T> GetAll() =>
        typeof(T).GetFields(BindingFlags.Public |
                            BindingFlags.Static |
                            BindingFlags.DeclaredOnly)
            .Select(field => field.GetValue(null))
            .OfType<T>();

    public override string ToString() => Name;

    // Comparación para ordenamiento
    public int CompareTo(T? other)
        => other is null ? 1 : Id.CompareTo(other.Id);

    // Búsqueda por ID
    public static T FromValue(int value)
    {
        var matchingItem = GetAll().FirstOrDefault(item => item.Id == value);

        return matchingItem
               ?? throw new InvalidOperationException($"'{value}' no es un valor válido para {typeof(T).Name}");
    }

    /// <summary>
    /// Búsqueda por nombre. Usa comparación ordinal: los nombres son identificadores
    /// del dominio, no texto sujeto a las reglas de la cultura del servidor.
    /// </summary>
    public static T FromName(string name)
    {
        var matchingItem = GetAll()
            .FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));

        return matchingItem
               ?? throw new InvalidOperationException($"'{name}' no es un nombre válido para {typeof(T).Name}");
    }

    // Operadores de comparación: un null se ordena antes que cualquier valor.
    public static bool operator <(Enumeration<T>? left, Enumeration<T>? right)
        => left is null ? right is not null : right is not null && left.Id < right.Id;

    public static bool operator <=(Enumeration<T>? left, Enumeration<T>? right)
        => left is null || (right is not null && left.Id <= right.Id);

    public static bool operator >(Enumeration<T>? left, Enumeration<T>? right)
        => right < left;

    public static bool operator >=(Enumeration<T>? left, Enumeration<T>? right)
        => right <= left;
}
