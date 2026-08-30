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
3. `src/Application/<Module>/<Action>/<Action>Command.cs` (o `Query.cs`); los read
   models / DTOs de salida van en `src/Application/<Module>/Contracts/<X>Dto.cs`
4. `src/Application/<Module>/<Action>/<Action>CommandValidator.cs`
5. `src/Application/<Module>/<Action>/<Action>UseCase.cs`
6. `src/Infrastructure/<Module>/EfCore/<Entity>Configuration.cs`
7. `src/Infrastructure/<Module>/EfCore/<Entity>Repository.cs` y
   `src/Infrastructure/<Module>/Sql/<Entity>ReadRepository.cs`
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

### Validación

Todo lo que **no necesita E/S** se valida en el `AbstractValidator<TCommand>`, no
en el caso de uso: forma y presencia, rangos numéricos, enum conocido (ambiente,
tipo e-CF, plan), reglas entre campos, reglas condicionales por campo
(`When(...)`) y cordura de fechas de calendario. El validador **puede** recibir
dependencias por constructor (p. ej. `TimeProvider` para "fecha no futura"); se
resuelven del contenedor como cualquier servicio.

El caso de uso solo se ocupa de lo que **sí** necesita E/S o contexto: resolver
el tenant actual, mapear los primitivos del comando a los tipos del dominio,
hechos de persistencia (unicidad y existencia contra la base), e invocar al
dominio. Las invariantes del agregado se quedan en el dominio (defensa en
profundidad); el validador es la puerta que hace que un request raro ni siquiera
llegue al `ExecuteCore`.

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
(`UseSnakeCaseNamingConvention`). Conviven los dos accesos, EF Core y Dapper. La
convención es **EF Core para escrituras, Dapper para lecturas**, con interfaces
separadas (`I<Entity>Repository` e `I<Entity>ReadRepository`).

- **Plomería** compartida en `src/Infrastructure/Persistence/` — `EfCore/`
  (`AppDbContext`, interceptores, `RowLevelSecurity`, migraciones, `EfCoreUnitOfWork`)
  y `Sql/` (`DbSession`, `DateTimeOffsetHandler`).
- **Implementación por módulo** en `src/Infrastructure/<Module>/EfCore/`
  (configuración + repositorio de escritura) y `src/Infrastructure/<Module>/Sql/`
  (repositorio de lectura). Igual que `Dgii/` y `Signing/`: cada módulo agrupa su
  infraestructura bajo `src/Infrastructure/<Module>/`.

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

**Firma XMLDSig** (`src/Infrastructure/Signing/XmlDsigSigner.cs`): `IXmlSigner`
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

**Generación XML del e-CF** (`src/Domain/Ecf`, `src/Infrastructure/Ecf`): `EcfDocument`
(dominio) valida estructura y calcula totales con Módulo 6. `IEcfXmlSerializer`
produce el `<ECF>` con el orden exacto del XSD, sin tags vacíos, escape DGII (8
caracteres), formato numérico y fechas `dd-MM-yyyy`; incluye `<FechaHoraFirma>`
pero no `<Signature>`. `IEcfXsdValidator` valida contra los XSD oficiales
**vendorizados y embebidos** en `src/Infrastructure/Ecf/Xsd/`. v1: **los diez tipos**
(31–34, 41, 43–47) con **todos los bloques del formato** (InformacionesAdicionales,
Transporte, OtraMoneda, Subtotales, DescuentosORecargos/Sección D, Paginacion,
desglose ImpuestosAdicionales, Mineria, TablaSubcantidad…). Particularidades: 41 y
47 con `<Retencion>` obligatoria por línea (47 solo ISR); 43 el más reducido (sin
`<Comprador>`); 43/44/47 solo líneas exentas; 45 ≡ 31; 46 solo tasa 0 %. Los
bloques transversales son **passthrough** (el cliente trae los montos), salvo la
Sección D que el motor reconcilia. **Solo falta el formato reducido RFCE**
(32 &lt; DOP 250 k). No cambiar sin leer `docs/ecf-xml.md`. `EcfDocument` **no es**
el payload de la API (ese es curado — `docs/api-ecf.md`).

**Motor de cálculo fiscal** (`src/Domain/Fiscal`): dominio puro, sin E/S ni reloj.
`EcfCalculator.Calculate(lines)` da `<MontoItem>` por línea y todos los
totalizadores del Encabezado. `EcfRounding` (regla DGII: mitad hacia afuera del
cero; 2 dec dinero, 4 dec precio unitario y tipo de cambio, 3 dec subcantidad).
`ItbisRate` (indicadores 1–4). `CreditNoteIndicator` — regla de los 30 días para
tipo 34, **valores 0/1** (el Plan Técnico dice 1/2: está mal). La tolerancia de
cuadratura **nunca rechaza** (RF-06.6). Las retenciones de ITBIS/ISR se
**totalizan** (montos por línea que trae el cliente → `<TotalITBISRetenido>` /
`<TotalISRRetencion>`; no tocan `<MontoTotal>`). La **Sección D**
(descuentos/recargos globales) se **reconcilia mecánicamente**: el monto se aplica
al bucket que indica `AffectsRate` y se recalcula su ITBIS; la Norma 10-07 solo
baja el `<ValorPagar>`. El **cálculo** de las tasas de retención, el ISC de
alcoholes/cigarrillos y la distribución de la Sección D a nivel de línea son slices
posteriores. No cambiar sin leer `docs/fiscal.md`.

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

## Documentación

Todo `.md` nuevo va en `docs/` y debe quedar listado en la carpeta de solución
`/docs/` de `NovaFE.slnx` para verse en el Solution Explorer de Visual Studio. De
eso se encarga un hook `PostToolUse` (`.claude/hooks/sync-docs-slnx.py`): al
escribir un `docs/*.md` inserta su `<File Path="docs/<nombre>.md" />` ordenado
alfabéticamente. Es idempotente. Si editas el `.slnx` a mano, mantené ese orden.


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
