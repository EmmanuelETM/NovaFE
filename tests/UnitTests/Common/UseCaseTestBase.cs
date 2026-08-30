using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace NovaFE.UnitTests.Common;

/// <summary>
/// Base para probar casos de uso: trae listo el <c>ILoggerFactory</c> que exige
/// la clase base y un reloj controlable.
/// <para>
/// Una prueba unitaria no toca base de datos ni HTTP: los repositorios se
/// sustituyen con NSubstitute. Si necesitas infraestructura real, la prueba va
/// en el proyecto de integración.
/// </para>
/// </summary>
public abstract class UseCaseTestBase
{
    /// <summary>Logger que no escribe nada; el caso de uso solo necesita que exista.</summary>
    protected static NullLoggerFactory LoggerFactory { get; } = new();

    /// <summary>
    /// Reloj fijo. Permite afirmar sobre fechas sin que la prueba dependa del
    /// momento en que se ejecuta.
    /// </summary>
    protected FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 1, 15, 10, 30, 0, TimeSpan.Zero));
}
