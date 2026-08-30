using ErrorOr;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace NovaFE.Service.Common;

[ApiController]
public abstract class ApiController : ControllerBase
{
    protected IActionResult Problem(List<Error> errors)
    {
        if (errors.Count == 0)
            return Problem();

        if (errors.All(e => e.Type == ErrorType.Validation))
        {
            var modelState = new ModelStateDictionary();
            foreach (var error in errors)
                modelState.AddModelError(error.Code, error.Description);

            return ValidationProblem(modelState);
        }

        var first = errors[0];

        var statusCode = first.Type switch
        {
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status500InternalServerError
        };

        var traceId = HttpContext.Items["TraceId"]?.ToString() ?? HttpContext.TraceIdentifier;

        var problemResult = Problem(
            statusCode: statusCode,
            title: first.Description,
            detail: errors.Count > 1 ? "Multiple errors occurred. Check the errors list for details." : null
        );

        if (problemResult is ObjectResult { Value: ProblemDetails problemDetails })
        {
            problemDetails.Extensions["traceId"] = traceId;

            if (errors.Count > 1)
            {
                problemDetails.Extensions["errors"] = errors.Select(e => new { e.Code, e.Description });
            }
        }

        return problemResult;
    }
}