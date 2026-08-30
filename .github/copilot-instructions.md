# Instrucciones para GitHub Copilot

API ASP.NET Core con Clean Architecture. La infraestructura ya está construida:
el trabajo es agregar lógica de negocio siguiendo los patrones existentes.

`CLAUDE.md`, en la raíz, tiene la versión extendida de estas reglas.

## Capas

`Service → Application → Domain`. `Infrastructure` implementa las interfaces que
declara `Application`; `Application` nunca referencia a `Infrastructure`, y
`Domain` no referencia a nadie.

## Reglas

- Los casos de uso heredan de `CommandUseCase<,>` o `QueryUseCase<,>` y
  sobrescriben `ExecuteCore`. **Nunca** incluyen `try/catch`, logging, llamadas
  al validador ni códigos HTTP: las clases base ya lo resuelven.
- Los casos de uso y los validadores **no se registran a mano**;
  `AddApplication()` los descubre por reflexión. Solo los repositorios se
  registran, en `InfrastructureService.cs`.
- Los errores se **devuelven** con `ErrorOr<T>`, no se lanzan. El `ErrorType`
  (`NotFound`, `Conflict`, `Validation`…) determina el código HTTP. Se declaran
  en `src/Domain/<Modulo>/<Entidad>Errors.cs`.
- Los controllers heredan de `ApiController`, llevan
  `[Route("api/v{version:apiVersion}/[controller]")]` y su cuerpo es
  `(await useCase.Execute(request, ct)).Match(Ok, Problem)`.
- Nunca `DateTime.UtcNow`: inyecta `TimeProvider`. Nunca `HttpContext` fuera de
  `Service`: inyecta `ICurrentUser`.
- Todo método de E/S es `async` y propaga `CancellationToken ct`.
- La base de datos es PostgreSQL (Npgsql). EF Core para escrituras, Dapper para
  lecturas.
- Con EF Core, la auditoría y el borrado lógico los aplican interceptores y un
  filtro global: no los escribas a mano. Con Dapper sí hay que hacerlos
  explícitos (no hay interceptores).
- La carpeta de Dapper se llama `Sql` a propósito: un namespace terminado en
  `.Dapper` rompe el `using Dapper;`.
- Las versiones de paquetes van en `Directory.Packages.props`; los
  `<PackageReference>` de los `.csproj` no llevan `Version`.

## Pruebas

xUnit v3, NSubstitute y **Shouldly** (no FluentAssertions). Las pruebas de
integración heredan de `IntegrationTestBase` y usan `[RequiresDockerFact]` en
lugar de `[Fact]`. Si agregas una tabla, actualiza
`tests/IntegrationTests/Fixtures/schema.sql`.
