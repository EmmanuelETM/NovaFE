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

Siempre en este orden, un archivo por concepto. El módulo **`Tenants`** es el
vertical slice de referencia — cópialo.

1. `src/Domain/<Module>/<Entity>.cs` y `<Entity>Errors.cs`
2. `src/Application/<Module>/Interfaces/I<Entity>Repository.cs` (escritura, EF) e
   `I<Entity>ReadRepository.cs` (lectura, Dapper)
3. `src/Application/<Module>/<Action>/<Action>Command.cs` (o `Query.cs`) y sus
   read models si es query
4. `src/Application/<Module>/<Action>/<Action>CommandValidator.cs`
5. `src/Application/<Module>/<Action>/<Action>UseCase.cs`
6. `src/Infrastructure/Persistence/EfCore/Configurations/<Entity>Configuration.cs`
7. `src/Infrastructure/Persistence/EfCore/Repositories/<Entity>Repository.cs` y
   `.../Sql/Repositories/<Entity>ReadRepository.cs`
8. Registrar **solo los repositorios** en `src/Infrastructure/InfrastructureService.cs`
9. Migración: `dotnet dotnet-ef migrations add <Name> --project src/Infrastructure
   --startup-project src/Service` (necesita `ASPNETCORE_ENVIRONMENT=Development`
   para el connection string). Si la entidad es `ITenantOwned`, llamar a
   `RowLevelSecurity.Enable(migrationBuilder, "<table>")` en el `Up`.
10. `src/Service/Controllers/<Module>Controller.cs`
11. Pruebas: unitarias del dominio y del caso de uso; integración del endpoint.

Nombres en **inglés** (identificadores, columnas, tests); español solo en
comentarios y en lo que sale de la API. Ver la regla de idioma más abajo.

---

## Idioma

Regla global del servicio. Todo lo **interno** va en **inglés**: nombres de
clases, métodos, variables, parámetros, namespaces, archivos, entidades y
propiedades de EF, nombres de tests, columnas y tablas (snake_case en inglés),
los `code` de los `Error`, las plantillas de log.

El **español** es solo para lo que mira hacia afuera:

- **comentarios** y documentación XML;
- todo lo que la API **emite**: descripciones de `Error`, títulos y detalles de
  ProblemDetails, mensajes de validación, texto de webhooks y respuestas, texto de
  la Representación Impresa.

El scaffold todavía tiene identificadores en español (`Aplicar`, `conexion`,
`EscribirRespuesta`…); se dejan salvo que se toque ese código.

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

La base de datos es **PostgreSQL** (proveedor Npgsql), esquema **snake_case**
(`UseSnakeCaseNamingConvention`). Conviven los dos accesos:
`src/Infrastructure/Persistence/EfCore` (EF Core) y `src/Infrastructure/Persistence/Sql`
(Dapper). La convención es **EF Core para escrituras, Dapper para lecturas**, con
interfaces separadas (`I<Entity>Repository` e `I<Entity>ReadRepository`).

- **Migraciones EF Core**, no `schema.sql`. `dotnet-ef` está en el manifiesto de
  herramientas (`dotnet tool restore`). Las pruebas de integración corren las
  migraciones sobre el contenedor.
- **Lecturas Dapper**: aliasear las columnas al nombre del parámetro del record
  (`legal_name AS "LegalName"`); si no, Dapper busca un constructor con parámetros
  snake_case. Hay un `DateTimeOffsetHandler` para leer `timestamptz`.

### Multi-tenant (esquema compartido + RLS)

La mayoría de las tablas son de un tenant. Ver `docs/multi-tenancy.md`. En corto:

- Entidad con datos de cliente → implementa `ITenantOwned` (`Guid TenantId { get; private set; }`).
  `Tenant` **no** lo es: es la raíz.
- `ICurrentTenant` (Application) da el tenant de la petición; lo llena
  `TenantResolutionMiddleware` (hoy del header `X-Tenant-Id`).
- Aislamiento en 3 capas: filtro global de EF (`"Tenant"`, siempre), interceptor
  de escritura (`TenantStampingInterceptor`), y RLS en Postgres (producción; un
  superusuario la ignora, por eso el filtro de EF es la garantía en local/tests).
- Migración de tabla `ITenantOwned`: `RowLevelSecurity.Enable(migrationBuilder, "<table>")`.

**Caché** (`src/Infrastructure/Caching`): `AddCache()` registra `IDistributedCache`
**en memoria**. No hay Redis a propósito — a la escala de arranque es superficie
de mantenimiento sin beneficio (ver `docs/redis.md` para el porqué y los pasos de
vuelta). Consumir siempre vía interfaz de dominio (`IDgiiTokenCache`, etc.), nunca
`IDistributedCache` directo en casos de uso. **Idempotencia y lock de secuencias
e-NCF van a PostgreSQL, no a caché**: exigen durabilidad y unicidad.

**Vault de certificados** (`src/Infrastructure/Security`): el PKCS#12 se guarda
detrás de `ICertificateVault` (impl. por defecto: envelope encryption AES-256-GCM,
ciphertext en Postgres, KEK vía `IKeyProtector` desde config/KMS). Provider-agnóstico
a propósito — ver `docs/certificates.md`. Un `Certificate` solo guarda una
`VaultReference` opaca.

**Firma XMLDSig** (`src/Infrastructure/Security/XmlDsigSigner.cs`): `IXmlSigner`
(cripto pura) e `ICertificateSigner` (orquesta vault + vigencia + firma). Los
parámetros de la DGII (C14N **estándar** no exclusivo, SHA-256, `Reference URI=""`,
`preserveWhitespace=false`, cert embebido) están fijos y afirmados en las pruebas.
No cambiar sin leer `docs/signing.md`.

**Autenticación DGII** (`src/Infrastructure/Dgii`): `IDgiiTokenProvider` da un
token Bearer del tenant actual para un ambiente — caché → semilla → firma →
validar → guardar, con renovación proactiva. `IDgiiAuthClient` (HTTP resiliente),
`IDgiiTokenCache` (sobre `IDistributedCache`). Ver `docs/dgii-auth.md`. El token
nunca va a base de datos.

**Secuencias e-NCF** (`src/Domain/Sequences`, `src/Application/Sequences`): el
inventario de rangos autorizados por la DGII. `EcfType` (los diez tipos, en
`Domain/Common`) y `Encf` (value object de 13 caracteres). `NcfSequence`
(`ITenantOwned`) deriva el vencimiento (31-dic del año siguiente, salvo tipos 32 y
34) y entrega números con `Allocate(today)`. `INcfSequenceAllocator` hace la
asignación **atómica** con `SELECT … FOR UPDATE` (raw SQL vía
`FromSql(...).IgnoreQueryFilters()`, dentro de `CreateExecutionStrategy` +
`BeginTransactionAsync`). El lock va a PostgreSQL, nunca a caché. v1 usa solo el
puntero `Next`; el pool de secuencias liberadas y el ciclo de vida por secuencia
son slices posteriores. No cambiar sin leer `docs/sequences.md`.

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
- **Almacenamiento y lógica interna: instantes UTC** (`DateTimeOffset`, columnas
  `timestamptz`). No cambiar. La **hora dominicana** (`DominicanTimeZone`,
  UTC-4 fijo) es solo para los bordes: lo que se serializa a la DGII, la RI, y
  la API (un converter global la aplica a todo `DateTimeOffset` de salida). La
  aritmética de fechas de calendario (vencimiento e-NCF, regla de 30 días de NC,
  contingencia) se hace con `timeProvider.GetDominicanToday()`, no en UTC. Ver
  `docs/time.md`.
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
- Las aserciones son con **Shouldly** (`result.ShouldBe(...)`).
- El esquema de las pruebas de integración sale de las migraciones de EF Core
  (`DatabaseFixture` corre `Database.MigrateAsync` sobre el contenedor). Al
  agregar una entidad, genera su migración; no hay `schema.sql`.

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
