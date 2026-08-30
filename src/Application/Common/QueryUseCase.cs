using ErrorOr;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace NovaFE.Application.Common;

/// <summary>
/// Caso de uso de <b>lectura</b>: no modifica estado.
/// Implementa <c>ExecuteCore</c> con la consulta.
/// </summary>
public abstract class QueryUseCase<TRequest, TResponse>(
    ILoggerFactory loggerFactory,
    IValidator<TRequest>? validator = null)
    : UseCaseBase<TRequest, TResponse>("Query", loggerFactory, validator);

/// <summary>
/// Query sin parámetros de entrada (por ejemplo, un catálogo completo).
/// Permite llamar <c>Execute(ct)</c> sin construir un request vacío.
/// </summary>
public abstract class ParameterlessQueryUseCase<TResponse>(ILoggerFactory loggerFactory)
    : QueryUseCase<NoRequest, TResponse>(loggerFactory)
{
    public Task<ErrorOr<TResponse>> Execute(CancellationToken ct = default)
        => Execute(NoRequest.Instance, ct);
}
