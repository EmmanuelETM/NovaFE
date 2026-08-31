# NovaFE

API ASP.NET Core con Clean Architecture.

La infraestructura ya está resuelta: manejo de errores, validación, logging,
observabilidad, versionado, salud, rate limiting, persistencia y pruebas. Lo que
queda por escribir es la lógica de negocio.

---

## Arrancar

### Opción A: todo con Docker (no requiere nada instalado salvo Docker)

```bash
docker compose up --build
```

Levanta dos contenedores: `api` y `postgres` (16).

| Servicio | Host | Notas |
| --- | --- | --- |
| API | <http://localhost:8080> | `/scalar` para la documentación interactiva |
| PostgreSQL | `localhost:5432` | usuario `postgres`, base `NovaFE`, contraseña `Local_dev_password_123` |

La contraseña de Postgres y el entorno se sobrescriben con variables de entorno
(`POSTGRES_PASSWORD`, `ASPNETCORE_ENVIRONMENT`); copia `.env.example` a `.env`.

No hay Redis: a la escala de arranque la caché es en memoria. Para meterlo cuando
haga falta, ver [`docs/redis.md`](docs/redis.md).

Herramientas de inspección opcionales (perfil `tools`):

```bash
docker compose --profile tools up
```

añade **pgweb** en <http://localhost:8081>.

Compose crea la **base vacía**, no las tablas: eso depende de la estrategia que
elijas (ver [Base de datos](#base-de-datos)). Hasta que apliques el esquema,
`/health/ready` responde 200 pero los endpoints devuelven 500 con
`relation does not exist`. Con migraciones de EF Core:

```bash
dotnet ef database update --project src/Infrastructure --startup-project src/Service \
  --connection "Host=localhost;Port=5432;Database=NovaFE;Username=postgres;Password=Local_dev_password_123"
```

### Opción B: API local, dependencias en Docker

Levanta solo la base de datos y corre la API desde el IDE o la CLI:

```bash
docker compose up postgres
dotnet run --project src/Service
```

`appsettings.Development.json` ya apunta a `localhost:5432`.
Para no dejar credenciales en el repositorio usa user-secrets:

```bash
dotnet user-secrets set "ConnectionStrings:Default" "Host=...;Database=NovaFE;..." --project src/Service
```

### Opción C: Visual Studio

Abre `NovaFE.slnx` y elige el proyecto de arranque **docker-compose**: F5 levanta
toda la orquestación con depuración (Fast Mode) sobre el contenedor `api`. El
proyecto `NovaFE.Service` también trae el perfil **Container (Dockerfile)** para
depurar solo la API en su contenedor.

La documentación interactiva queda en `/scalar` (solo en Development).

---

## Estructura

```
src/
├── Domain/           Entidades, errores de dominio, tipos base. Sin dependencias.
├── Application/      Casos de uso, contratos (DTOs) e interfaces de repositorio.
├── Infrastructure/   Persistencia, clientes HTTP. Implementa lo que Application declara.
└── Service/          Controllers, middlewares, Program.cs. El composition root.

tests/
├── UnitTests/        Dominio y casos de uso, sin infraestructura.
└── IntegrationTests/ La API completa contra un PostgreSQL real (Testcontainers).

src/Service/Dockerfile       Imagen de la API (base → build → publish → final).
docker-compose.yml           Orquestación: api + postgres.
docker-compose.override.yml  Comodidades de desarrollo (puertos, perfil "tools").
docker-compose.dcproj        Proyecto de orquestación para Visual Studio.
.env.example                 Variables sobrescribibles; cópialo a .env.
docs/redis.md                Cómo y cuándo añadir Redis (no está por ahora).
```

La regla de dependencias apunta siempre hacia adentro: `Service` → `Application`
→ `Domain`. `Infrastructure` implementa interfaces de `Application`, pero
`Application` nunca la referencia. Eso es lo que permite probar los casos de uso
sin base de datos.

---

## Agregar una funcionalidad

Para una funcionalidad nueva, siempre en este orden, un archivo por concepto:

1. **Domain** — la entidad y sus errores:

   ```csharp
   // src/Domain/Solicitudes/Solicitud.cs
   public sealed class Solicitud : Entity<int>, IAuditableEntity, ISoftDeletable { ... }

   // src/Domain/Solicitudes/SolicitudErrors.cs
   public static Error NoEncontrada(int id) => Error.NotFound(...);
   ```

2. **Application** — el contrato del repositorio, el request, el validador y el
   caso de uso:

   ```csharp
   // src/Application/Solicitudes/Interfaces/ISolicitudRepository.cs
   // src/Application/Solicitudes/CrearSolicitud/CrearSolicitudCommand.cs
   // src/Application/Solicitudes/CrearSolicitud/CrearSolicitudCommandValidator.cs
   // src/Application/Solicitudes/CrearSolicitud/CrearSolicitudUseCase.cs
   public sealed class CrearSolicitudUseCase(...)
       : CommandUseCase<CrearSolicitudCommand, int>(loggerFactory, validator)
   {
       protected override async Task<ErrorOr<int>> ExecuteCore(
           CrearSolicitudCommand request, CancellationToken ct) { ... }
   }
   ```

3. **Infrastructure** — la implementación del repositorio, registrada en
   `InfrastructureService.cs`.

4. **Service** — el controller:

   ```csharp
   [ApiVersion("1")]
   [Route("api/v{version:apiVersion}/[controller]")]
   public sealed class SolicitudesController(CrearSolicitudUseCase crear) : ApiController
   {
       [HttpPost]
       public async Task<IActionResult> Crear([FromBody] CrearSolicitudCommand cmd, CancellationToken ct)
           => (await crear.Execute(cmd, ct)).Match(id => CreatedAtAction(...), Problem);
   }
   ```

**Los casos de uso y los validadores no se registran a mano.** `AddApplication()`
descubre por reflexión todo lo que implemente `IUseCase<,>` y todo `IValidator<T>`
del ensamblado. Solo los repositorios se registran explícitamente.

---

## Lo que ya está resuelto (y que no debes reimplementar)

| Necesidad | Dónde vive | Qué no tienes que escribir |
| --- | --- | --- |
| Validación de entrada | `UseCaseBase` + FluentValidation | Llamar al validador en cada caso de uso |
| Errores → HTTP | `ErrorOr` + `ApiController.Problem` | `try/catch` y `StatusCode(...)` en controllers |
| Errores no controlados | `GlobalExceptionHandler` + ProblemDetails | Middleware de excepciones |
| Logging de entrada/salida y duración | `UseCaseBase` | `_logger.LogInformation` al inicio y fin |
| Correlación de requests | `TraceIdMiddleware` | Propagar un id entre capas |
| Auditoría (`CreatedAt`, `CreatedBy`, …) | Interceptor de EF Core | Asignar los campos en cada `Add` |
| Borrado lógico | Interceptor + filtro global | `WHERE IsDeleted = false` en cada consulta de EF |
| Transacciones | `IUnitOfWork.ExecuteInTransactionAsync` | `BeginTransaction` / `Commit` / `Rollback` |
| Reintentos y circuit breaker HTTP | `AddResilientHttpClient` | Configurar Polly |
| Paginación | `PagedRequest` / `PagedResult` | Validar y acotar `PageSize` |
| Tiempo | `TimeProvider` inyectado | `DateTime.UtcNow` (imposible de probar) |
| Usuario actual | `ICurrentUser` | Leer claims desde `HttpContext` |

---

## Endpoints de infraestructura

| Ruta | Para qué sirve |
| --- | --- |
| `/health/live` | ¿El proceso está vivo? No toca la base de datos. Úsalo como *liveness probe*. |
| `/health/ready` | ¿Puede atender tráfico? Verifica PostgreSQL. Úsalo como *readiness probe*. |
| `/health` | Todas las verificaciones, con detalle. |
| `/scalar` | Documentación interactiva (solo Development). |
| `/openapi/v1.json` | Documento OpenAPI de la v1 (solo Development). |

La distinción entre *live* y *ready* importa: si el orquestador usa una sonda que
consulta la base de datos para decidir si reiniciar el contenedor, una caída
momentánea de PostgreSQL provoca un reinicio en cascada de toda la flota.

---

## Configuración

| Sección | Qué controla |
| --- | --- |
| `ConnectionStrings:Default` | PostgreSQL. **Obligatorio**: si está vacío, la API no arranca. |
| `Database` | Timeout de comandos, reintentos, logging detallado de SQL. |
| `Cors:AllowedOrigins` | Orígenes permitidos. Vacío = ningún origen externo. |
| `RateLimiting` | Límite por usuario/IP. Desactivado en Development. |
| `Observability:OtlpEndpoint` | Destino OpenTelemetry. Vacío = no se exporta nada. |
| `Serilog` | Niveles y destinos de log. |

La validación de configuración corre al arrancar (`ValidateOnStart`): un
connection string faltante falla de inmediato con un mensaje claro, en vez de
reventar en el primer request de un usuario.

---

## Autenticación

Es un hueco intencional. Cada servicio decide su esquema. En `Program.cs`
(sección 6) hay un bloque comentado con la configuración de JWT Bearer y los tres
pasos para habilitarlo.

`ICurrentUser` ya lee los claims y funciona aunque no haya autenticación
configurada, así que **los casos de uso no cambian** cuando la habilites.

---

## Pruebas

```bash
dotnet test
```

Las pruebas de integración levantan un PostgreSQL real en un contenedor
(Testcontainers), le aplican `tests/IntegrationTests/Fixtures/schema.sql` y
resetean los datos entre pruebas con Respawn. **Requieren Docker en ejecución**;
si no lo hay, se omiten en vez de fallar.

Para las pruebas unitarias, `UseCaseTestBase` ya trae un `ILoggerFactory` nulo y
un `FakeTimeProvider`, de modo que el tiempo es determinista.

---

## Base de datos

El esquema no se crea solo. Tienes dos caminos, y conviene elegir uno:

- **Migraciones de EF Core**:

  ```bash
  dotnet ef migrations add Inicial --project src/Infrastructure --startup-project src/Service
  dotnet ef database update --project src/Infrastructure --startup-project src/Service
  ```

- **Scripts SQL versionados**, aplicados por el pipeline de despliegue.

En cualquier caso, mantén `tests/IntegrationTests/Fixtures/schema.sql` al día:
es lo que define la base contra la que corren las pruebas de integración.
