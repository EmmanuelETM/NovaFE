namespace NovaFE.IntegrationTests.Fixtures;

/// <summary>
/// Agrupa todas las pruebas de integración en una sola colección para que
/// compartan el contenedor de PostgreSQL. Sin esto, xUnit correría las clases
/// en paralelo y cada una levantaría su propio contenedor.
/// </summary>
[CollectionDefinition(nameof(IntegrationTestCollection))]
public sealed class IntegrationTestCollection : ICollectionFixture<DatabaseFixture>;
