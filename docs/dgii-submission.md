# Envío a la DGII y seguimiento (Módulo 4)

Lleva un `IssuedEcf` firmado (Módulo 12) hasta su estado fiscal: lo envía a los
web services de la DGII, guarda el `TrackId` y consulta el resultado. La
durabilidad la da un **outbox sobre PostgreSQL** — sin broker, atómico con la
persistencia del comprobante.

Fuentes: `C:\workplace\FE_DGII\Plan Técnico Integral v2.0.txt` §5 (RF-04.x) y
`contexto-proyecto-fe-dgii.md` §5.10 B–G (endpoints reales). Donde difieren, manda
el contexto.

## Endpoints de la DGII que consumimos

| Qué | Método | Ruta | Respuesta |
|---|---|---|---|
| Enviar `<ECF>` | POST | `{ecfBase}/{amb}/recepcion/api/facturaselectronicas` (multipart `xml`) | `{ trackId, error, mensaje }` |
| Enviar `<RFCE>` (tipo 32 < DOP 250 k) | POST | `{fcBase}/{amb}/recepcionfc/api/recepcion/ecf` (multipart `xml`) | `{ codigo, estado, mensajes[], encf, secuenciaUtilizada }` — **síncrono** |
| Consultar resultado | GET | `{ecfBase}/{amb}/consultaresultado/api/consultas/estado?trackid=X` | `{ codigo, estado, secuenciaUtilizada, fechaRecepcion, mensajes[] }` |

`{ecfBase}` = `Dgii:EcfBaseUrl` (`https://ecf.dgii.gov.do`); `{fcBase}` =
`Dgii:FcBaseUrl` (`https://fc.dgii.gov.do`) — **dominio distinto**. `{amb}` =
`DgiiEnvironment.UrlSegment`. Todas llevan `Authorization: Bearer` del tenant
(`IDgiiTokenProvider`, `docs/dgii-auth.md`).

Códigos de la DGII: `0` no encontrado (puede seguir en proceso), `1` aceptado,
`2` rechazado (nulidad), `3` en proceso, `4` aceptado condicional (tiene validez).
`secuenciaUtilizada = false` → el e-NCF se puede reutilizar; `true`/null → quemado.

## Piezas

| Interfaz (Application) | Impl (Infrastructure) | Rol |
|---|---|---|
| `IDgiiSubmissionClient` | `DgiiSubmissionClient` | HTTP puro (el Bearer entra por parámetro). Dos clientes resilientes con nombre, uno por dominio. Fallos de red → `Errors.Http.*`. |
| `IEcfSubmissionQueue` | `PostgresEcfSubmissionQueue` | Outbox sobre `ecf_submission_outbox`. Reclamo `FOR UPDATE SKIP LOCKED` + `locked_by` único por llamada. |
| — | `EcfSubmissionProcessor` (Application) | El **único** code path del envío. `ProcessAsync` (worker, con ladder) y `PollOnceAsync` (fast-path, sin ladder). |
| `IEcfSubmissionFastPath` | `EcfSubmissionFastPath` (Application) | El "síncrono" del `POST /ecf`. |
| `IEcfSubmissionPump` | `EcfSubmissionPump` (Service) | Un tick: reap + claim + por-fila scope con el tenant fijado. |
| — | `EcfSubmissionWorker : BackgroundService` (Service) | Dispara el pump en intervalo (jitter, multi-instancia seguro). |

## Máquina de estados (`EcfStatus`)

```
signed ──(fast-path inline / worker)──► submitted ──► accepted
   │                                        │      └─► accepted_conditional
   │  (fallo de transporte, agota backoff)  │      └─► rejected
   └──────────────► failed ◄────────────────┤
                       │                    └─► review  (ladder de polling agotado)
                       └──(POST /ecf/{id}/retry)──► signed
```

- `signed` = firmado y **encolado**. `submitted` = enviado, hay `TrackId`.
- Terminales: `accepted`, `accepted_conditional`, `rejected`.
- `review` (la DGII no resolvió tras el ladder) y `failed` (agotó el backoff de
  transporte, o el gateway rechazó la recepción) se reencolan con
  `POST /ecf/{id}/retry`.
- Las transiciones son métodos del agregado (`IssuedEcf.Mark*`); una transición
  inválida → `IssuedEcf.InvalidTransition`.

## Fast-path síncrono del `POST /ecf`

Tras firmar y persistir (+ encolar, en la misma transacción), el request intenta
resolver contra la DGII dentro de `EcfSubmission:SyncWaitBudgetSeconds` (~8 s): un
envío + hasta `MaxInlinePolls` consultas rápidas. Si la DGII resuelve, la
respuesta `201` lleva `status: accepted` / `rejected` / `accepted_conditional`. Si
no, `201` con `status: submitted` o `signed` y el worker termina. **Nunca** falla
el `POST` por la DGII.

## Outbox (`ecf_submission_outbox`)

Tabla de **sistema**: no es `ITenantOwned`, **sin RLS** (cola operativa; lleva
`tenant_id`/`ecf_id` solo para reconstruir contexto). `kind` = `submit` | `poll`;
`status` = `pending` | `processing` | `done` | `dead`. El reaper devuelve a
`pending` las filas atascadas en `processing` más de `StuckAfterMinutes`.

El claim es un `UPDATE ... SET status='processing', locked_by=<token> WHERE id IN
(SELECT ... FOR UPDATE SKIP LOCKED LIMIT n)` seguido de un `SELECT WHERE
locked_by=<token>` — reclama y suelta el lock enseguida, sin transacción abierta
durante la llamada HTTP a la DGII.

## Ladders (RF-04.3 / RF-04.7)

- **Polling** (worker): +30 s (primera), luego +5 min, +30 min, +30 min. Al
  agotarse → `review` + `LogWarning`.
- **Backoff de envío** ante fallos de transporte: 2 min → 10 min → 30 min → 2 h.
  Al agotarse → `failed` + `LogError`. El gateway sin `TrackId` (XSD, firmante no
  autorizado…) no se reintenta: `failed` directo.

Los tiempos viven en `EcfSubmissionOptions` (sección `EcfSubmission`); los de las
capas internas se proyectan a `EcfSubmissionSettings`.

## Contexto de tenant en el worker

Fuera de una petición `ICurrentTenant` es null → el filtro global de EF oculta
todo (`docs/multi-tenancy.md`). El pump reclama el lote sin tenant (el outbox no
lleva RLS) y procesa **cada fila en su propio scope** con
`CurrentTenant.Set(item.TenantId)`, así el repositorio y el token quedan acotados
al tenant correcto.

## Configuración (`EcfSubmission`)

| Clave | Default | |
|---|---|---|
| `Enabled` | `true` | Arranca el worker. `false` en pruebas. |
| `PollIntervalSeconds` | `5` | Ticks del worker. |
| `BatchSize` | `25` | Filas por tick. |
| `StuckAfterMinutes` | `5` | Umbral del reaper. |
| `SyncWaitBudgetSeconds` | `8` | Presupuesto del fast-path (`0` lo desactiva). |
| `MaxInlinePolls` | `3` | Consultas rápidas del fast-path. |
| `InlinePollDelayMillis` | `600` | Espera entre esas consultas. |
| `FirstPollDelaySeconds` | `30` | Primera consulta del worker tras el envío. |

## Fuera de alcance (módulos propios)

Webhooks (slice aparte — el cliente hace polling de `GET /ecf/{id}`), envío al
**receptor electrónico** B2B (M5), contingencia / `IndicadorEnvioDiferido` (M11),
polling de `consultaestatusservicio` (M10), anulación ANECF y estado `voided`
(M8), liberación de secuencias quemadas por un rechazo (un rechazo simplemente
quema el número — `docs/sequences.md`), carga manual del XML del tipo 32 en el
portal DGII (acción del operador).

## Pendiente de verificar contra TesteCF real

Probado end-to-end contra WireMock. Contra TesteCF hay que confirmar:

1. Nombres/formato exactos de los campos de las tres respuestas (`trackId`,
   `codigo`, `mensajes[].codigo` — la DGII lo devuelve como número o cadena).
2. Comportamiento del código `0` (lo tratamos como "sigue en proceso").
3. Que el RFCE realmente resuelve síncrono en `codigo` (y qué devuelve si no).
4. La ruta exacta de `recepcionfc` (`/api/recepcion/ecf` vs. variantes).
