# Observabilidad y métricas de performance

Investigación (2026-09-14) sobre qué tan visible es el comportamiento en
producción de NovaFE hoy, qué falta, y qué decisiones de infraestructura hay
que tomar antes de seguir agregando instrumentación. No es un plan de
implementación — es el mapa para decidir por dónde arrancar.

## Estado actual

| Pieza | Dónde | Qué cubre |
|---|---|---|
| Trazas + métricas genéricas | `src/Service/Extensions/ObservabilityExtensions.cs` (OpenTelemetry) | ASP.NET Core (duración/conteo de requests), `HttpClient` (incluye las llamadas a la DGII), Npgsql (**solo trazas**), runtime .NET (GC, thread pool, excepciones) |
| Logs | Serilog → `stdout`, correlacionados por `TraceId` (`TraceIdMiddleware`) | Todo lo que se loguea explícitamente en el código |
| Health checks | `src/Service/Extensions/HealthCheckExtensions.cs` | `/health/live` (proceso vivo), `/health/ready` (Postgres alcanzable, timeout 5s) |
| Config | `Observability:OtlpEndpoint` (`ObservabilityOptions.cs`) | Si está vacío, la instrumentación corre igual pero **no se exporta nada** |

No existe ningún `Meter`/`ActivitySource` propio en el código — cero métricas
de negocio. Todo lo de arriba es instrumentación automática de librerías.

## El hallazgo que precede a cualquier métrica nueva

`Observability:OtlpEndpoint` está vacío por default **en todos lados**,
incluido el propio `deploy/main.bicep` (`param otlpEndpoint string = ''`, línea
52). Es decir: en cualquier ambiente desplegado hoy, toda la instrumentación
genérica de arriba se genera y se descarta — no hay ningún colector
configurado en ningún lado. Antes de agregar una sola métrica nueva hay que
decidir a dónde va esto (ver **Decisiones de infraestructura** más abajo);
si no, lo nuevo tiene el mismo destino que lo viejo: a ningún lado.

## Por qué los "golden signals" genéricos no alcanzan acá

Latencia HTTP, throughput, errores y GC no dicen nada sobre los puntos donde
**este sistema en particular** puede fallar bajo carga. No son riesgos
hipotéticos — son consecuencia directa de decisiones de diseño ya tomadas y
documentadas:

1. **Asignación de secuencias e-NCF** (`NcfSequence.Allocate`,
   `SELECT ... FOR UPDATE` — `docs/sequences.md`): lock pesimista por
   (tenant, tipo, serie). El caso de uso real que motivó
   [[ecf-duplicate-detection]] (ráfaga de facturas idénticas en minutos
   durante inscripción universitaria) es exactamente el escenario de
   contención — y hoy es invisible.
2. **Dependencia externa DGII**: tiene circuit breaker (tuneado, ver
   `docs/dgii-submission.md` §Resiliencia), pero su estado no se puede ver.
   `AddStandardResilienceHandler` expone callbacks de apertura/cierre de
   Polly que hoy no están conectados a nada — te enterás de que se abrió por
   los logs de error, no por una métrica.
3. **Los outbox** (`ecf_submission_outbox`, `webhook_deliveries`): un patrón
   outbox vive y muere por una pregunta — ¿cuántas filas pendientes hay y
   hace cuánto que la más vieja espera? Hoy solo se responde con una query
   manual a Postgres.
4. **Workers en `BackgroundService`**: si `EcfSubmissionWorker` muere por una
   excepción fuera del try/catch por-fila (en `ClaimBatchAsync` o
   `ReapStuckAsync`), el proceso sigue reportando `/health/ready` = OK
   mientras no procesa nada. No hay heartbeat.
5. **Fast-path síncrono** (`EcfSubmission:SyncWaitBudgetSeconds` ~8s, ver
   `docs/dgii-submission.md`): no se sabe qué fracción de las emisiones
   resuelve en el fast-path vs cae al worker — es la métrica de UX más
   directa de ese diseño y no existe.
6. **Firma XMLDSig + validación XSD + render de PDF (QuestPDF)**: las tres
   operaciones CPU-bound dentro del request síncrono de emisión. Bajo ráfaga
   son las primeras en degradar la latencia p99.
7. **Npgsql solo tiene trazas, no métricas** — falta `metrics.AddNpgsql()`
   junto al `tracing.AddNpgsql()` que ya existe. Sin eso no hay visibilidad
   del pool de conexiones (agotamiento de pool es causa clásica de
   incidentes que ninguna traza individual muestra).
8. **Sampling de trazas**: no hay sampler configurado (default del SDK =
   100 %). Mientras no se exporta nada no importa; en cuanto haya colector,
   es costo de exportación sin control.

## Métricas recomendadas (custom `Meter`, hoy no existe ninguno)

Convención de nombre sugerida: `novafe.<área>.<nombre>`. **Regla de
cardinalidad**: nunca taguear por `tenant_id` (no acotado, va a crecer) — usar
`plan_tier` (5 valores conocidos, ver [[pricing]]) donde haga falta desglosar;
el debugging por-tenant queda para logs estructurados + trazas (Serilog ya
tiene el contexto del tenant).

### Emisión de e-CF

| Métrica | Tipo | Tags | Por qué |
|---|---|---|---|
| `ecf.issued` | counter | `type`, `environment`, `result` | Throughput de negocio real, no solo requests HTTP |
| `ecf.sequence_allocation.duration` | histogram | — | Tiempo bajo el lock `FOR UPDATE`; el único cuello de botella verdaderamente propio del dominio |
| `ecf.fastpath.outcome` | counter | `resolved` \| `timed_out` | Si rara vez resuelve, la mayoría de los clientes esperan el budget completo para nada |
| `ecf.signing.duration` | histogram | — | Costo de la operación de firma (incluye el vault de certificados) |
| `ecf.xsd_validation.duration` | histogram | — | Validación del XML firmado contra el XSD, en el camino síncrono |
| `representation.render.duration` | histogram | `layout` | Render de PDF con QuestPDF, también síncrono |

### DGII / outbox de envío

| Métrica | Tipo | Tags | Por qué |
|---|---|---|---|
| `dgii.outbox.depth` | gauge | `status` | Señal operacional #1 del Módulo 4 |
| `dgii.outbox.oldest_pending_age` | gauge (s) | — | Mejor que `depth` solo: dice si está estancado, no solo cuánto hay |
| `dgii.circuitbreaker.state` | gauge (0/1/2) | `client` | Conectar los callbacks de Polly que ya existen pero no se usan |
| `dgii.submission.duration` | histogram | `outcome` | Separado del `HttpClient` genérico, para no mezclar "enviar" con "consultar estado" con "pedir token" |
| `dgii.token.refreshed` | counter | — | Renovar token implica una firma completa; conviene saber cuán seguido pasa |

### Webhooks

| Métrica | Tipo | Tags | Por qué |
|---|---|---|---|
| `webhooks.outbox.depth` / `.oldest_pending_age` | gauge | `status` | Mismo patrón que el outbox de la DGII |
| `webhooks.delivery.attempt` | counter | `attempt_number`, `outcome` | Visualizar que el backoff ladder funciona de verdad |
| `webhooks.endpoint.auto_disabled` | counter | — | Hoy es silencioso: un tenant deja de recibir notificaciones y nadie se entera hasta que reclama |

### Idempotencia y duplicados

| Métrica | Tipo | Tags | Por qué |
|---|---|---|---|
| `idempotency.hit` / `.miss` / `.conflict` | counter | — | Un `conflict` alto es señal de bug de cliente, no de NovaFE |
| `ecf.duplicate_suspected` | counter | `mode` | El evento de dominio ya existe (front 5); exportarlo como métrica es casi gratis |

### Motor de settings

| Métrica | Tipo | Tags | Por qué |
|---|---|---|---|
| `settings.snapshot.age` | gauge (s) | — | Toda la promesa de "los cambios propagan en N segundos" (poll de generación, [[configuration-model]]) hoy no se puede verificar en producción |

### Base de datos

- ~~Agregar `metrics.AddNpgsql()` junto al `tracing.AddNpgsql()` existente~~ —
  **intentado y descartado**: `Npgsql.OpenTelemetry` 10.0.3 solo expone
  `AddNpgsql()` en `TracerProviderBuilder`, no en `MeterProviderBuilder`; el
  paquete instalado no publica métricas de pool, solo trazas. Si esto importa
  lo suficiente, la alternativa sería instrumentar el pool a mano (Npgsql
  expone contadores vía `NpgsqlDataSource`) — no se hizo, es más esfuerzo que
  un quick win.
- Se agregó en cambio `SlowQueryLoggingInterceptor`
  (`src/Infrastructure/Persistence/EfCore/Interceptors/`): un
  `DbCommandInterceptor` que loguea en `Warning` cualquier comando SQL que
  supere `observability.slow_query_threshold_ms` (setting runtime, default
  500 ms). No es lo mismo que ver el pool de conexiones, pero cubre la mitad
  más barata del problema — una query específica que se puso lenta.

## Health checks: liveness de los workers — HECHO

Implementado: `IWorkerHeartbeat`/`WorkerHeartbeat` (`src/Service/Workers/`),
un singleton en memoria. Cada uno de los 5 workers
(`EcfSubmissionWorker`, `WebhookDeliveryWorker`, `ExpiryMonitorWorker`,
`RetentionWorker`, `SettingsGenerationPoller`) llama a `heartbeat.Beat(nombre,
maxSilence)` después de cada tick (exitoso o no — lo que importa es que el
`await` volvió, no que haya tenido éxito). `maxSilence` no es un umbral
global: cada worker lo deriva de su propio intervalo configurado (`×3` de
margen) porque un worker de segundos y uno de horas
(`ExpiryMonitorWorker`/`RetentionWorker`) no son comparables con el mismo
número.

`WorkerLivenessHealthCheck` compara `ahora - último latido` contra el
`maxSilence` de cada worker y reporta `Unhealthy` si alguno lo excede. Se
registra **sin** el tag `"ready"` a propósito: un worker atascado no impide
que la API sirva tráfico (mismo criterio que la DGII caída — se degrada, no
se cae), así que no debe sacar la instancia del balanceador ni reiniciar el
contenedor. Solo es visible en `/health` (no en `/health/live` ni
`/health/ready`).

Cubierto por tests unitarios
(`tests/UnitTests/Service/Workers/WorkerHeartbeatTests.cs`,
`WorkerLivenessHealthCheckTests.cs`).

## Settings de performance genuinamente tuneables

No todo lo de arriba debería ser un runtime setting — la mayoría es
visibilidad de solo lectura (métricas), no una perilla operativa. Lo que sí
vale la pena como setting o config:

- **Umbral de request lento** — HECHO: `observability.slow_request_threshold_ms`
  (setting runtime, `IntegerSetting`, default 1000 ms). `Program.cs` sube el
  log de `UseSerilogRequestLogging` de `Information` a `Warning` cuando
  `elapsed` lo supera, leyéndolo de `ISettingsReader` vía
  `httpContext.RequestServices` dentro del propio callback `GetLevel`. (Esta
  idea salió de comparar contra los settings de ECFGateway —
  `Performance.SlowRequestThresholdMs`.)
- **Umbral de query lenta en EF Core** — HECHO: `observability.slow_query_threshold_ms`
  (default 500 ms), consumido por `SlowQueryLoggingInterceptor` (ver arriba).
- **`Observability:TraceSamplingRatio`** — **no implementado todavía**, config de **bootstrap**, no
  runtime setting (mismo criterio que `RateLimitOptions`: cambiar el sampling
  en caliente no tiene el mismo valor que cambiarlo con un redeploy, y no es
  algo que un operador deba poder tocar sin pensar). Default 1.0 hoy no es
  problema porque no se exporta nada; sí lo será en cuanto haya un colector
  real recibiendo datos a volumen de producción.

## Decisiones de infraestructura pendientes

Estas no se resuelven con código — son decisiones del usuario, varias con
costo o con implicancias de arquitectura (mismo tipo de decisión que ya se
tomó explícitamente para Redis, ver `docs/redis.md`):

1. **¿A qué colector OTLP exportar?** Opciones típicas: Grafana Cloud
   (managed, tiene free tier), un OpenTelemetry Collector propio +
   Tempo/Mimir/Loki auto-hospedado (más control, más infra que mantener —
   tensiona con la filosofía "sin infra extra a propósito" de
   `docs/redis.md`), Uptrace, Application Insights (si se vuelve a Azure de
   lleno), Datadog/New Relic (vendor con costo por volumen). Esto define
   `Observability:OtlpEndpoint` en cada ambiente.
2. **¿Logs centralizados?** Hoy van a `stdout` únicamente. `docs/deployment.md`
   y `docs/vps-migration.md` ya sugieren un colector (Grafana Alloy, Vector,
   Promtail) pero no está implementado en ningún lado.
3. **¿Sampling en producción?** Una vez exista un colector, decidir el
   `TraceSamplingRatio` según volumen esperado y costo del backend elegido.
4. **¿Alerting real, y a quién?** Ninguna de las métricas de arriba sirve de
   nada sin un umbral que dispare una alerta y un canal (email, Slack,
   PagerDuty…). Ejemplo concreto que ya se puede definir hoy: *"el outbox de
   envío no debe tener una fila pendiente hace más de 15 minutos"*. Esto es
   trabajo de infra/ops, no de código — pero las métricas de este documento
   son el requisito previo.
5. **¿Dashboards?** Una vez el colector reciba datos, arma los tableros
   (Grafana boards u equivalente). Trabajo de infra, no de código.
6. **¿Esto empuja hacia un vendor o hacia más auto-hospedado?** Relacionado
   con la decisión de stack de deployment ya documentada
   (`docs/deployment.md` = Azure Container Apps + Neon;
   `docs/vps-migration.md` = alternativa self-hosted). La respuesta a (1)
   probablemente debería ser consistente con esa decisión, no independiente.

## Estado: quick wins — HECHO (2026-09-14)

Implementados los tres elegidos: heartbeat de workers + health check de
liveness, y los umbrales de request/query lento como settings runtime. El
intento de `metrics.AddNpgsql()` se descartó por limitación del paquete
instalado (ver la sección de Base de datos, arriba). 650 unit + 173
integration tests en verde.

**Lo que sigue sin construir** (todo lo demás de este documento): las
métricas de negocio custom (`Meter` propio — emisión, outbox, DGII,
webhooks, idempotencia, settings), y sobre todo las **decisiones de
infraestructura** de la sección anterior — sin resolver esas, ninguna métrica
nueva tiene a dónde ir.
