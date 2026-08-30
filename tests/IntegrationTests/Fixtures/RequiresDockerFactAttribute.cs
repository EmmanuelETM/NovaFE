using System.Runtime.CompilerServices;

namespace NovaFE.IntegrationTests.Fixtures;

/// <summary>
/// Igual que <c>[Fact]</c>, pero omite la prueba cuando no hay Docker disponible
/// en lugar de fallarla.
/// </summary>
/// <example>
/// <code>
/// [RequiresDockerFact]
/// public async Task Crear_devuelve_201() { ... }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequiresDockerFactAttribute : FactAttribute
{
    // Los parámetros con [CallerFilePath]/[CallerLineNumber] los exige xUnit v3
    // para poder reportar la ubicación de la prueba en el explorador.
    public RequiresDockerFactAttribute(
        [CallerFilePath] string? sourceFilePath = null,
        [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        if (!DockerAvailability.IsAvailable)
            Skip = "Requiere Docker en ejecución para levantar PostgreSQL.";
    }
}
