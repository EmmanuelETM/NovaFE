using NovaFE.Application.Common.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace NovaFE.Service.Middlewares;

/// <summary>
/// Traduce cualquier excepción no controlada a una respuesta ProblemDetails
/// consistente con la que produce <see cref="Common.ApiController"/>.
/// </summary>
public class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // Mismo origen de traceId que ApiController, para que un error de negocio
        // y una excepción no controlada se puedan correlacionar igual en los logs.
        var traceId = httpContext.Items["TraceId"]?.ToString() ?? httpContext.TraceIdentifier;

        var (statusCode, title, detail) = exception switch
        {
            ValidationException => (
                StatusCodes.Status400BadRequest,
                "Validation Error",
                "One or more validation errors occurred."),

            UnauthorizedAccessException => (
                StatusCodes.Status401Unauthorized,
                "Unauthorized",
                "You do not have permission to access this resource."),

            NotFoundException ex => (
                StatusCodes.Status404NotFound,
                "Not Found",
                ex.Message),

            ConflictException ex => (
                StatusCodes.Status409Conflict,
                "Conflict",
                ex.Message),

            OperationCanceledException => (
                StatusCodes.Status499ClientClosedRequest,
                "Client Closed Request",
                "The request was cancelled by the client."),

            _ => (
                StatusCodes.Status500InternalServerError,
                "Internal Server Error",
                "An unexpected error occurred.")
        };

        if (statusCode >= StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Error no controlado. TraceId: {TraceId}", traceId);
        else
            logger.LogWarning("Error de cliente {StatusCode}. TraceId: {TraceId}. Mensaje: {Message}", statusCode, traceId, exception.Message);

        httpContext.Response.StatusCode = statusCode;

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = $"{httpContext.Request.Method} {httpContext.Request.Path}"
        };

        problemDetails.Extensions["traceId"] = traceId;

        // Si es una excepción de validación, se expone el mismo diccionario "errors"
        // que genera ValidationProblem() en los controllers.
        if (exception is ValidationException valEx)
        {
            problemDetails.Extensions["errors"] = valEx.Errors
                .GroupBy(x => x.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(x => x.ErrorMessage).ToArray());
        }

        // Se escribe a través de IProblemDetailsService para respetar la configuración
        // central de AddProblemDetails() en lugar de serializar a mano.
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problemDetails,
            Exception = exception
        });
    }
}
