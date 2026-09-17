using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace NovaFE.Service.Security;

/// <summary>
/// Fase 5: mientras el principal lleve <c>impersonated_by</c> (un operador
/// viendo la API como otro usuario, ver <see cref="InternalKeyAuthenticationHandler"/>),
/// solo se permite leer. Un solo punto de corte por verbo HTTP — no hay que
/// tocar cada caso de uso para hacer la impersonación de solo lectura de
/// verdad, no de nombre.
/// </summary>
internal sealed class ImpersonationReadOnlyFilter : IAsyncActionFilter
{
    private static readonly HashSet<string> WriteMethods =
        new(StringComparer.OrdinalIgnoreCase) { "POST", "PUT", "PATCH", "DELETE" };

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (WriteMethods.Contains(context.HttpContext.Request.Method)
            && context.HttpContext.User.HasClaim(c => c.Type == SecuritySchemes.ImpersonatedByClaim))
        {
            context.Result = new ObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Modo impersonación: solo lectura.",
                Detail = "Un operador viendo la API como otro usuario no puede hacer cambios.",
            })
            {
                StatusCode = StatusCodes.Status403Forbidden,
            };
            return;
        }

        await next();
    }
}
