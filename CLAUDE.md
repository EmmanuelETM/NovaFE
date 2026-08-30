# Guía para agentes de IA

Este proyecto parte de una base de Clean Architecture ya construida. La
infraestructura está resuelta; el trabajo es agregar lógica de negocio
siguiendo los patrones existentes.

Contexto del producto y de negocio: ver la sección **Contexto del Proyecto** más
abajo. El detalle regulatorio de DGII vive en `C:\workplace\FE_DGII\`.

---

## Arquitectura

Cuatro capas, con las dependencias apuntando hacia adentro:

```
Service ──► Application ──► Domain
              ▲
Infrastructure┘   (implementa las interfaces que declara Application)
```

**Invariantes que no se rompen:**

- `Domain` no referencia a nadie. Nada de EF Core, ASP.NET ni paquetes de
  infraestructura ahí.
- `Application` **no** referencia a `Infrastructure`. Declara interfaces de
  repositorio; `Infrastructure` las implementa.
- Los controllers no contienen lógica de negocio. Ejecutan un caso de uso y
  hacen `Match`.
- La lógica de negocio no vive en los repositorios. Los repositorios solo
  persisten y consultan.

---

## Cómo se agrega una funcionalidad

Siempre en este orden, un archivo por concepto:

1. `src/Domain/<Modulo>/<Entidad>.cs` y `<Entidad>Errors.cs`
2. `src/Application/<Modulo>/Interfaces/I<Entidad>Repository.cs`
3. `src/Application/<Modulo>/<Accion>/<Accion>Command.cs` (o `Query.cs`)
4. `src/Application/<Modulo>/<Accion>/<Accion>CommandValidator.cs`
5. `src/Application/<Modulo>/<Accion>/<Accion>UseCase.cs`
6. `src/Infrastructure/<Modulo>/.../<Entidad>Repository.cs`
7. Registrar **solo el repositorio** en `src/Infrastructure/InfrastructureService.cs`
8. `src/Service/Controllers/<Modulo>Controller.cs`

---

## Reglas concretas

### Casos de uso

Heredan de `CommandUseCase<TRequest, TResponse>` (escritura) o
`QueryUseCase<TRequest, TResponse>` (lectura) y sobrescriben `ExecuteCore`.

**No escribas dentro de un caso de uso:** `try/catch`, llamadas al validador,
logs de entrada/salida, medición de duración ni códigos HTTP. `UseCaseBase` ya
hace todo eso. Un caso de uso que tenga un `try/catch` genérico está mal.

**No los registres en el contenedor.** `AddApplication()` descubre por reflexión
todo lo que implemente `IUseCase<,>` y todo `IValidator<T>`. Agregar un
`services.AddScoped<MiUseCase>()` es redundante.

### Errores

Se devuelven, no se lanzan. El tipo de retorno es `ErrorOr<T>`:

```csharp
if (yaExiste)
    return SolicitudErrors.CodigoDuplicado(codigo);   // Error.Conflict → 409
```

El `ErrorType` determina el código HTTP en `ApiController.Problem`. Un caso de
uso nunca menciona un status code. Los errores de cada módulo se declaran en
`src/Domain/<Modulo>/<Entidad>Errors.cs`.

### Persistencia

La base de datos es **PostgreSQL** (proveedor Npgsql). Conviven los dos accesos:
`src/Infrastructure/Persistence/EfCore` (EF Core) y `src/Infrastructure/Persistence/Sql`
(Dapper). La convención es **EF Core para escrituras, Dapper para lecturas**, con
interfaces separadas (`I<Entidad>Repository` e `I<Entidad>ReadRepository`).

**Caché** (`src/Infrastructure/Caching`): `AddCache()` registra `IDistributedCache`
**en memoria**. No hay Redis a propósito — a la escala de arranque es superficie
de mantenimiento sin beneficio (ver `docs/redis.md` para el porqué y los pasos de
vuelta). Consumir siempre vía interfaz de dominio (`IDgiiTokenCache`, etc.), nunca
`IDistributedCache` directo en casos de uso. **Idempotencia y lock de secuencias
e-NCF van a PostgreSQL, no a caché**: exigen durabilidad y unicidad.

- La carpeta de Dapper se llama `Sql`, no `Dapper`, porque un namespace terminado
  en `.Dapper` rompe el `using Dapper;`. No la renombres.
- Con EF Core, la auditoría y el borrado lógico los aplican interceptores y un
  filtro global: **no** asignes `CreatedAt`/`CreatedBy` a mano ni agregues
  `WHERE IsDeleted = false`.
- Con Dapper **sí** hay que hacer las dos cosas explícitamente (asignar
  `CreatedAt`/`CreatedBy` y filtrar `WHERE IsDeleted = false`), porque no hay
  interceptores.
- Para operaciones que abarcan varios repositorios, usa
  `IUnitOfWork.ExecuteInTransactionAsync`. No hay `SaveChangesAsync` en la
  interfaz: los repositorios persisten de inmediato.

### Tiempo, usuario y configuración

- Nunca `DateTime.UtcNow` ni `DateTime.Now`. Inyecta `TimeProvider` y usa
  `timeProvider.GetUtcNow()`; así las pruebas controlan el reloj.
- Nunca leas `HttpContext` fuera de `Service`. Para el usuario actual, inyecta
  `ICurrentUser`.
- La configuración se lee con el patrón Options (una clase en
  `src/Service/Configuration` o `Persistence/DatabaseOptions.cs`), no con
  `configuration["clave"]` desperdigado.

### Controllers

```csharp
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public sealed class SolicitudesController(CrearSolicitudUseCase crear) : ApiController
```

Heredan de `ApiController`, no de `ControllerBase` directamente. Todo endpoint
recibe un `CancellationToken` y lo propaga. La ruta lleva siempre el segmento de
versión; el versionado no asume una versión por defecto a propósito.

### Async

Todo método que toque E/S es `async` y recibe `CancellationToken ct`, que se
pasa hasta la consulta. Nada de `.Result`, `.Wait()` ni `async void`.

---

## Pruebas

- **Unitarias** (`tests/UnitTests`): dominio y casos de uso, con los repositorios
  sustituidos por NSubstitute. Hereda de `UseCaseTestBase`.
- **Integración** (`tests/IntegrationTests`): la API completa contra PostgreSQL
  real en Testcontainers. Hereda de `IntegrationTestBase` y marca las pruebas con
  `[RequiresDockerFact]`, no con `[Fact]`, para que se omitan si no hay Docker.
- Las aserciones son con **Shouldly** (`resultado.ShouldBe(...)`).
- Si agregas una tabla, agrégala también a
  `tests/IntegrationTests/Fixtures/schema.sql`.

---

## Comandos

```bash
dotnet build NovaFE.slnx              # compila; los warnings son errores
dotnet test NovaFE.slnx               # unitarias + integración (requiere Docker)
dotnet run --project src/Service      # levanta la API
docker compose up --build             # API + PostgreSQL en contenedores
docker compose --profile tools up     # además: pgweb (8081)
```

La raíz del repo tiene el `.slnx` y `docker-compose.dcproj`, así que los comandos
`dotnet` sin argumento de proyecto necesitan `NovaFE.slnx` explícito.

Las versiones de paquetes están centralizadas en `Directory.Packages.props`. Un
`<PackageReference>` en un `.csproj` **no** lleva atributo `Version`: se agrega
la versión en `Directory.Packages.props` y la referencia sin versión en el
proyecto.


## Contexto del Proyecto

# NovaFE — e-CF para República Dominicana

Leer antes de empezar:
- `C:\workplace\FE_DGII\contexto-proyecto-fe-dgii.md` — contexto completo del proyecto
- `C:\workplace\FE_DGII\Plan Técnico Integral v2.0.txt` — especificaciones técnicas

Stack: ASP.NET Core 10 · PostgreSQL 16 · Clean Architecture
Proveedor EF Core: Npgsql 10.0.3
Outbox pattern sobre PostgreSQL (`SKIP LOCKED`) para emisión asíncrona de e-CF —
sin broker dedicado; Redis tampoco (caché en memoria, ver `docs/redis.md`)
Multi-tenant con Row-Level Security en PostgreSQL
