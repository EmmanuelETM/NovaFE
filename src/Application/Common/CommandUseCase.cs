using ErrorOr;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace NovaFE.Application.Common;

/// <summary>
/// Caso de uso de <b>escritura</b>: crea, modifica o elimina estado.
/// Implementa <c>ExecuteCore</c> con la lógica de negocio.
/// </summary>
public abstract class CommandUseCase<TRequest, TResponse>(
    ILoggerFactory loggerFactory,
    IValidator<TRequest>? validator = null)
    : UseCaseBase<TRequest, TResponse>("Command", loggerFactory, validator);

/// <summary>
/// Command que no devuelve datos, solo éxito o error.
/// </summary>
public abstract class CommandUseCase<TRequest>(
    ILoggerFactory loggerFactory,
    IValidator<TRequest>? validator = null)
    : CommandUseCase<TRequest, Success>(loggerFactory, validator);
