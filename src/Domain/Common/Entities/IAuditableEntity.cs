namespace NovaFE.Domain.Common.Entities;

/// <summary>
/// Entidad con rastro de auditoría. Estas propiedades <b>no se llenan a mano</b>:
/// el interceptor de persistencia las escribe usando el usuario actual y el reloj
/// inyectado, en cada guardado.
/// </summary>
public interface IAuditableEntity
{
    DateTimeOffset CreatedAt { get; set; }

    string? CreatedBy { get; set; }

    DateTimeOffset? UpdatedAt { get; set; }

    string? UpdatedBy { get; set; }
}
