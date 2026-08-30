namespace NovaFE.Domain.Common.Entities;

/// <summary>
/// Entidad que nunca se borra físicamente. Al eliminarla se marca como borrada
/// y desaparece de todas las consultas automáticamente.
/// <para>
/// Para incluir los registros borrados en una consulta concreta, usa
/// <c>IgnoreQueryFilters()</c> en EF Core, o escribe el WHERE explícito en Dapper.
/// </para>
/// </summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }

    DateTimeOffset? DeletedAt { get; set; }

    string? DeletedBy { get; set; }
}
