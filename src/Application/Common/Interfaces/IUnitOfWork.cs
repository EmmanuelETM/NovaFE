namespace NovaFE.Application.Common.Interfaces;

/// <summary>
/// Agrupa varias operaciones de repositorio en una sola transacción atómica.
/// <para>
/// <b>No lo necesitas para una sola escritura</b>: los repositorios persisten de
/// inmediato. Úsalo cuando dos o más operaciones deban confirmarse o revertirse juntas.
/// </para>
/// <para>
/// La implementación depende del ORM elegido al crear el proyecto, pero el contrato
/// es el mismo: si la operación lanza, se hace rollback; si termina, se hace commit.
/// </para>
/// </summary>
/// <example>
/// <code>
/// return await _unitOfWork.ExecuteInTransactionAsync(async token =>
/// {
///     var id = await _solicitudes.AddAsync(solicitud, token);
///     await _bitacora.RegistrarAsync(id, token);
///     return id;
/// }, ct);
/// </code>
/// </example>
public interface IUnitOfWork
{
    /// <summary>Ejecuta la operación dentro de una transacción y devuelve su resultado.</summary>
    Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken ct = default);

    /// <summary>Ejecuta la operación dentro de una transacción.</summary>
    Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken ct = default);
}
