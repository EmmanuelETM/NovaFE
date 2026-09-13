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

## Lo que ve el cliente

`GET /ecf/{id}` (y la respuesta del `POST`) traen el `status` de NovaFE y un
objeto **`dgii`** con el intercambio: `trackId`, `status` (el `estado` textual),
`statusCode` (1/2/3/4), `sequenceUsed`, `messages[]` y los instantes
`submittedAt` / `receivedAt` (la `fechaRecepcion` de la DGII) / `processedAt`. Es
`null` hasta que hay envío. Internamente el agregado guarda los mismos datos en
columnas planas (`track_id`, `dgii_status_code`, `dgii_status_text`,
`sequence_usable`, `dgii_messages` jsonb, `submitted_at`, `dgii_received_at`,
`dgii_processed_at`) más `submission_attempts` (que no se expone). La
`fechaRecepcion` llega en hora dominicana y se guarda como instante UTC como todo
lo demás. Detalle del contrato en `docs/api-ecf.md` §6.

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

## Resiliencia (circuit breaker)

Los tres clientes HTTP de la DGII (`IDgiiAuthClient` y los dos nombrados
`dgii-ecf`/`dgii-fc` de `DgiiSubmissionClient`) usan
`.AddStandardResilienceHandler()` (`Microsoft.Extensions.Http.Resilience`).
Defaults de la librería:

| Estrategia | Default |
|---|---|
| Circuit breaker | `FailureRatio` 10%, `MinimumThroughput` 100, `SamplingDuration` 30s, `BreakDuration` 5s |
| Retry | 3 intentos, backoff exponencial + jitter, 2s |
| Attempt timeout | 10s |
| Total request timeout | 30s |

`MinimumThroughput = 100` no tiene sentido al volumen real de tráfico contra
la DGII (por tenant, facturación) — el circuito nunca junta 100 peticiones en
30s ni sumando todos los tenants, así que quedaba efectivamente decorativo:
nunca abre aunque la DGII esté completamente caída. Se overridea vía
`Dgii:Resilience` (sección que bindea `HttpStandardResilienceOptions`
completo — cualquier sub-propiedad no listada sigue en el default de la
librería):

| Clave (`Dgii:Resilience:CircuitBreaker`) | Valor | Por qué |
|---|---|---|
| `MinimumThroughput` | `5` | Representativo a la escala de tráfico actual — el breaker es compartido por *todos* los tenants del cliente nombrado, así que una caída real de la DGII genera varios fallos casi simultáneos. |
| `FailureRatio` | `0.5` | Con una muestra tan chica, un ratio bajo (10%) abriría el circuito por un par de fallos aislados. La mitad de un puñado de intentos fallando es más honesto. |
| `BreakDuration` | `30s` | 5s no vale la pena contra un servicio realmente caído; 30s no choca con el fast-path del `POST /ecf` (`SyncWaitBudgetSeconds` ~8s, corre antes de que el circuito llegue a abrirse en el caso normal). |
| `SamplingDuration` | `30s` (sin cambio) | Cumple la validación de la librería (`≥ 2×AttemptTimeout`, que sigue en 10s). |

`Retry`/`AttemptTimeout`/`TotalRequestTimeout` quedan en los defaults de la
librería — no forman parte de este ajuste.

Cada cliente nombrado (`dgii-auth`, `dgii-ecf`, `dgii-fc`) tiene su **propio**
circuito — comparten los mismos valores de configuración, pero no se
fusionan: una caída del dominio `fc.dgii.gov.do` no abre el circuito de
`ecf.dgii.gov.do`.

`HttpErrorMapper` ya traduce `BrokenCircuitException` (circuito abierto) a
`Errors.Http.CircuitOpen` — ver `src/Infrastructure/Http/HttpErrorMapper.cs`.
Esto **no** es contingencia (M11, `IndicadorEnvioDiferido`): solo evita
martillar un servicio caído; qué hace la app "en contingencia" sigue fuera de
alcance (ver más abajo).

## Ladders (RF-04.3 / RF-04.7)

- **Polling** (worker): +30 s (primera), luego +5 min, +30 min, +30 min. Al
  agotarse → `review` + `LogWarning`.
- **Backoff de envío** ante fallos de transporte: 2 min → 10 min → 30 min → 2 h.
  Al agotarse → `failed` + `LogError`. El gateway sin `TrackId` (XSD, firmante no
  autorizado…) no se reintenta: `failed` directo.

Los dos ladders son settings **runtime** (`submission.poll_ladder` /
`submission.backoff`, grupo "Envío a la DGII" en `/api/v1/platform-settings`) —
antes estaban hardcoded en el default de `EcfSubmissionSettings` sin ningún
camino de configuración, ni siquiera bootstrap. `EcfSubmissionOptions.ToSettings`
los lee de `ISettingsReader` (formato: lista separada por comas, `5m`/`30m`/`2h`);
un valor corrupto cae al array hardcoded original, nunca lanza. El resto de los
tiempos siguen en `EcfSubmissionOptions` (sección `EcfSubmission`, bootstrap).

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
| `PollIntervalSeconds` | `10` | Ticks del worker **con trabajo**. |
| `MaxPollIntervalSeconds` | `60` | Techo del intervalo con la cola vacía: cada tick vacío duplica la espera hasta acá, y vuelve al base al procesar algo. |
| `StuckAfterMinutes` | `5` | Umbral del reaper (el reap corre a lo sumo 1×/min, no en cada tick). |
| `SyncWaitBudgetSeconds` | `8` | Presupuesto del fast-path (`0` lo desactiva). |
| `MaxInlinePolls` | `3` | Consultas rápidas del fast-path. |
| `InlinePollDelayMillis` | `600` | Espera entre esas consultas. |
| `FirstPollDelaySeconds` | `30` | Primera consulta del worker tras el envío. |

El tamaño de lote (`submission.batch_size`, filas por tick) y los dos ladders
de arriba viven ahora en `/api/v1/platform-settings` (grupo "Envío a la DGII"),
no en esta sección bootstrap.

## Fuera de alcance (módulos propios)

Envío al
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
