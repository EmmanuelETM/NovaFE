using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace NovaFE.Service.Common;

/// <summary>
/// Marca un controller que <b>solo</b> debe existir en el entorno de desarrollo
/// (herramientas de diagnóstico, previews, etc.). Fuera de Development,
/// <see cref="RemoveDevelopmentOnlyConvention"/> lo quita del modelo de la
/// aplicación: no rutea, no aparece en OpenAPI, no existe.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class DevelopmentOnlyAttribute : Attribute;

/// <summary>
/// Quita del modelo MVC todo controller marcado con <see cref="DevelopmentOnlyAttribute"/>.
/// Se registra en <c>Program.cs</c> únicamente cuando el entorno <b>no</b> es Development.
/// </summary>
public sealed class RemoveDevelopmentOnlyConvention : IApplicationModelConvention
{
    public void Apply(ApplicationModel application)
    {
        var toRemove = application.Controllers
            .Where(controller => controller.Attributes.OfType<DevelopmentOnlyAttribute>().Any())
            .ToList();

        foreach (var controller in toRemove)
            application.Controllers.Remove(controller);
    }
}
