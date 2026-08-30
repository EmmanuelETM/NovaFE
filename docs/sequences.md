# Gestión de secuencias e-NCF (Módulo 7)

Administra el inventario de rangos de e-NCF autorizados por la DGII a cada
contribuyente y entrega números de forma atómica bajo concurrencia.

Fuente: `C:\workplace\FE_DGII\Plan Técnico Integral v2.0.txt` §8 (RF-07.x) y
`contexto-proyecto-fe-dgii.md` §Vencimiento de secuencias.

## El e-NCF

Trece caracteres: `[Serie] + [Tipo, 2 díg.] + [Secuencial, 10 díg.]` — p. ej.
`E310000000001`.

- **Serie**: una letra de la `E` a la `Z`, **sin la `P`**. Varias series activas
  por tipo (RF-07.6).
- **Tipo**: uno de los diez códigos DGII (`EcfType`, en `Domain/Common`): 31, 32,
  33, 34, 41, 43, 44, 45, 46, 47.
- **Secuencial**: posición dentro del rango, de 1 en adelante.

`Encf` (value object, `Domain/Sequences`) parsea, valida y construye; se convierte
implícitamente a `string`.

## Vencimiento

`31 de diciembre del año siguiente` a la autorización — **salvo los tipos 32 y 34**
(`HasSequenceExpiry = false`, no llevan `FechaVencimientoSecuencia`). El cálculo
usa el calendario dominicano (`timeProvider.GetDominicanToday()`), no UTC. El
chequeo de vencimiento va **antes** que el de stock (RF-07.4).

## Reglas por ambiente

| Ambiente | Rango | |
|---|---|---|
| TesteCF | Configurable | — |
| CerteCF | 1 a 10 000 000 por tipo | El rango **debe empezar en 1** (`Sequence.CertEcfMustStartAtOne`) |
| eCF (producción) | Según autorización DGII | — |

## Piezas

| Interfaz (Application) | Impl (Infrastructure) | Rol |
|---|---|---|
| `INcfSequenceRepository` | `NcfSequenceRepository` (EF Core) | Escritura: alta de rangos, chequeo de serie activa duplicada. |
| `INcfSequenceReadRepository` | `NcfSequenceReadRepository` (Dapper) | Lectura: `capacity` y `remaining` se calculan en SQL; `IsLowStock` y `TypeName` en el read model. |
| `INcfSequenceAllocator` | `NcfSequenceAllocator` (EF Core) | **Asignación atómica** (RF-07.2). |

`NcfSequence` (agregado, `ITenantOwned`): un rango autorizado. `Authorize(...)`
deriva el vencimiento y valida serie/rango/reglas de CerteCF. `Allocate(today)`
entrega el siguiente número y avanza `Next` (solo avanza; nunca retrocede).
`Deactivate()` lo saca del inventario.

`RegisterSequenceRangeCommandValidator` es la puerta: forma, rango, ambiente y
tipo conocidos, serie válida, y —con `TimeProvider` inyectado— que la fecha de
autorización no sea futura; en CerteCF exige además `desde = 1` y `hasta ≤ 10M`.
Lo que sí necesita base (serie ya activa) y las invariantes del agregado se
quedan en el caso de uso y en `NcfSequence.Authorize` como defensa en profundidad.

## Asignación atómica

`NcfSequenceAllocator.AllocateAsync`:

1. `CreateExecutionStrategy().ExecuteAsync` + `BeginTransactionAsync` (igual que
   `EfCoreUnitOfWork`; obligatorio con `EnableRetryOnFailure`).
2. `SELECT * FROM ncf_sequences WHERE tenant_id = … AND environment = … AND
   ecf_type = … AND active AND NOT is_deleted ORDER BY series, range_from
   FOR UPDATE` vía `FromSql(...).IgnoreQueryFilters().ToListAsync()`.
   - El `FOR UPDATE` bloquea las filas: la segunda petición espera al commit de la
     primera antes de leer `Next`, así que **nunca comparten número**.
   - `IgnoreQueryFilters()` es imprescindible: si EF aplicara el filtro global de
     tenant / soft-delete, envolvería el SQL en una subconsulta y el `FOR UPDATE`
     dejaría de bloquear la fila real. Por eso el SQL filtra el tenant y el
     borrado lógico de forma explícita.
   - Sin operadores LINQ extra tras `FromSql`: cualquiera envuelve el SQL.
3. Descarta los rangos vencidos, y del primero con stock toma el número
   (`NcfSequence.Allocate`). Si el primero está agotado, pasa al siguiente
   (spill-over entre series).
4. `SaveChangesAsync` + `CommitAsync`. El `UPDATE` de `Next` viaja dentro de la
   transacción, manteniendo el lock hasta el commit.

**Idempotencia y durabilidad**: el lock vive en PostgreSQL, nunca en caché
(ver `CLAUDE.md` / `docs/redis.md`).

## Alcance de v1

- Solo puntero `Next`. Los huecos que dejan las secuencias no usadas o quemadas
  por un rechazo son aceptables (la DGII no exige uso contiguo).
- **Sin pool de secuencias liberadas** (reclamar rechazos con
  `secuenciaUtilizada = false`): es un slice posterior, junto con sus llamadores
  en el flujo de emisión.
- El ciclo de vida de cada secuencia asignada
  (`asignada → firmada → enviada_dgii → aceptada | rechazada`, RF-07.5) y la
  anulación de rangos (ANECF, Módulo 8) son slices aparte.

## Endpoints (`/api/v1.0/sequences`, por tenant)

| Método | Ruta | |
|---|---|---|
| `POST` | `/` | Registra un rango. Body: `environment`, `type`, `series`, `rangeFrom`, `rangeTo`, `authorizedOn?` (por defecto hoy). |
| `GET` | `/{id}` | Un rango con su stock derivado. |
| `GET` | `/` | Todos los rangos del tenant. |
| `POST` | `/allocate` | Toma la siguiente secuencia. Body: `environment`, `type`. Responde `{ encf, type, series, sequential }`. |

## Migración

`AddNcfSequences` — tabla `ncf_sequences`, índice único parcial
`(tenant_id, environment, ecf_type, series) WHERE active AND NOT is_deleted`
(cierra la ventana de carrera del alta duplicada), y
`RowLevelSecurity.Enable(migrationBuilder, "ncf_sequences")`.
