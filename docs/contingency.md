# Contingencia — M11 Tipo 1 (falta de conectividad)

**Estado: implementado solo el Tipo 1.** El Instructivo de Contingencia de FE
(`C:\workplace\FE_DGII\contexto-proyecto-fe-dgii.md` §5.9; base legal Ley
32-23, Norma 01-2020, Decreto 587-24 Arts. 40-43) describe tres tipos con
relojes distintos. Solo el **Tipo 1** (falta de conectividad) está
construido — es el único que el propio corte day-one del proyecto marca como
bloqueante de producción, y el único que la DGII permite automatizar sin
notificación previa. Tipo 2 (imposibilidad técnica, declaración manual vía
OFV, comprobantes Serie B) y Tipo 3 (caída de la propia DGII) quedan **fuera
de alcance**, ver abajo.

## Qué es (y qué NO es) el Tipo 1

Cuando el emisor no puede comunicarse con la DGII (falla propia, del
proveedor de internet, o de los servidores de la DGII), sigue generando sus
e-CF **normalmente** — firma local, misma estructura de XML — y los remite en
lote no más tarde de **72 horas después de restablecida la conexión**. No
requiere notificación previa a la DGII.

**El XML no cambia en nada.** `<IndicadorEnvioDiferido>` (`EcfHeader.DeferredDelivery`)
es un campo **distinto**, exclusivo de contribuyentes con autorización previa
y **permanente** de la DGII por su modelo de negocio (camiones repartidores
en rutas sin internet, ventas móviles con handheld) — sigue existiendo tal
cual, controlado por el cliente en el payload de `POST /ecf`, y **este slice
no lo toca**. La única diferencia observable de un e-CF emitido en
contingencia Tipo 1 es operativa (se guarda y reenvía en cuanto vuelve la
conexión) y de la Representación Impresa (leyenda obligatoria, ver abajo).

## Qué ya hacía el pipeline, y qué le faltaba

El envío a la DGII (Módulo 4, `docs/dgii-submission.md`) ya firma y persiste
localmente, y reintenta con backoff cuando la DGII no responde — exactamente
lo que pide el Tipo 1. El hueco real: tras agotar el ladder de backoff
(~2h48m), el comprobante caía en `failed` y quedaba esperando un
`POST /ecf/{id}/retry` manual. En una caída larga eso puede hacer perder la
ventana de 72h si nadie lo nota. Este slice cierra ese hueco (ver
"Reintento sin dar por perdido", abajo) y agrega la señal de estado + la
leyenda de la RI.

## `platform.contingency_mode`

Un solo booleano (`src/Domain/Settings/SettingDefinitions.cs`, `Sensitive`,
grupo "Plataforma"), con dos caminos para prenderlo — ambos escriben el mismo
valor, no hay un flag separado por origen:

- **Manual**: `PUT /api/v1/platform-settings/platform.contingency_mode`
  (`{ value: "true"|"false", confirm: true }`) — un operador lo fuerza a mano.
- **Automático**: `ContingencyMonitor` (Application) + `ContingencyMonitorWorker`
  (Service, `BackgroundService`, tick `ContingencyMonitor:IntervalSeconds`,
  default 60s). Cada tick revisa `IOpsStatusReadRepository.GetEcfSubmissionOutboxStatusAsync`
  — la misma señal Postgres-backed que ya alimenta la consola de operación
  (`/plataforma/operacion`) — y compara la antigüedad de la fila
  pendiente/en proceso más vieja contra `ContingencyMonitor:ActivationThresholdMinutes`
  (default 5 min, por encima del primer escalón del backoff de envío de
  ~2min para no activarse por un blip transitorio). Si cruza el umbral y el
  setting está apagado, lo prende; si ya no lo cruza y está prendido, lo
  apaga. Escribe con `UpdatePlatformSettingUseCase` directamente (Application,
  resoluble por DI, sin HTTP de por medio) — es el único punto donde se exige
  la confirmación de un setting `Sensitive`, y un detector automático no
  tiene pantalla donde pedirla.

**Por qué antigüedad del outbox y no el circuit breaker**: el circuit breaker
de los clientes HTTP de la DGII (`docs/dgii-submission.md` §Resiliencia) es
estado de Polly **en memoria, por instancia** — no compartido entre réplicas.
El outbox (`ecf_submission_outbox`) ya es Postgres, ya es la fuente de verdad
multi-instancia, y ya tiene una consulta probada
(`OpsStatusReadRepository.GetEcfSubmissionOutboxStatusAsync`, construida para
la consola de operación) que da exactamente la señal necesaria sin SQL nuevo.

## Reintento sin dar por perdido

`EcfSubmissionProcessor.OnTransportFailureAsync` (`src/Application/Ecf/Submission/`):
cuando el ladder de `submission.backoff` se agota, antes de marcar el
comprobante `failed` revisa `platform.contingency_mode`. Si está activo, no
da por perdido — reprograma con el último escalón del ladder repetido, sin
seguir incrementando `Attempts` más allá del tamaño del ladder, así vuelve a
caer en la misma rama la próxima vez. Cuando la contingencia se desactiva, el
próximo intento fallido sí agota el ladder normalmente. Esto es lo que en la
práctica cumple la regla de las 72h: mientras dure la contingencia, un
comprobante nunca se queda esperando un retry manual.

## `issued_ecf.signed_during_contingency`

Columna nueva (bool, default `false`), estampada en `IssuedEcf.FromSigned` a
partir de `platform.contingency_mode` leído en `IssueEcfUseCase.ExecuteCore`
justo antes de firmar — mismo patrón que `EcfDuplicateDetectionMode`. Como el
XML no lleva ninguna marca (ver arriba), es la única forma de saber después
"este comprobante en particular se firmó mientras la plataforma estaba en
contingencia". Sale en `EcfDto.signedDuringContingency` — el cliente tiene
interés legítimo en saberlo (la validez fiscal del comprobante queda sujeta a
la reserva de las 72h).

## Leyenda en la Representación Impresa (RF-09.5)

`RepresentationModel.ContingencyNotice` (ya existía, sin poblar) se arma en
`EcfXmlRepresentationReader.Read` a partir de `signedDuringContingency` (el
`GetEcfRepresentationUseCase` se lo pasa desde `EcfDto`, no desde el XML). El
texto es el prescrito por la DGII, verbatim, no parafrasear
(`EcfXmlRepresentationReader.ContingencyLegend`):

> "e-CF emitido en modalidad de Contingencia, el cual podrá ser consultado
> para su validez fiscal, a partir de las setenta y dos (72) horas."

Los dos layouts (Carta y POS) ya sabían imprimirlo si no era null.

## Webhooks

`contingency.activated` / `contingency.deactivated` — no hay un recurso único
de por medio (es un cambio de estado de plataforma, no de un e-CF puntual):
el `data.object` es `{ status: "active"|"inactive", changedAt, oldestPendingMinutes }`.
Se disparan desde `ContingencyMonitorPump` (Service), que recorre los tenants
activos (mismo patrón de loop que `ExpiryMonitorPump`) y hace *fan-out* a
cada uno suscrito — es un evento de plataforma, todos los suscritos deben
enterarse, no solo el tenant que tuvo la mala suerte de intentar un envío
primero. Ver `docs/webhooks.md`.

**Límite conocido, aceptado a propósito**: a diferencia de `ExpiryScan` (que
tiene un log de idempotencia por aviso), la transición de contingencia no
tiene un log propio — es el propio valor del setting (`previous != value`)
el que decide si hay algo que notificar. Si el proceso de fan-out se cae a
mitad de camino (algunos tenants notificados, otros no), el próximo tick del
monitor ya no vuelve a disparar el webhook para esa transición (el setting ya
quedó en el valor destino). Es una ventana angosta y de bajo impacto — el
mecanismo que de verdad importa (el reintento sin dar por perdido) no
depende de que el webhook llegue — así que no se justificó construir un log
de dedup para esto en v1.

## Consola de operación

`GET /api/v1/ops/status` expone `contingencyActive: bool`
(`OpsStatusDto`, poblado con el mismo `ISettingsReader` que el resto de la
pantalla). `/plataforma/operacion` pinta un aviso destacado cuando está
activa, aparte del veredicto algorítmico de workers/colas/secuencias — es un
*modo* de plataforma, no una anomalía transitoria, así que no se mezcló con
el sistema de "hechos por actor" que arma el titular (`hero.ts`).

## Piezas

| Pieza | Capa | Rol |
|---|---|---|
| `ContingencyMonitor` | Application (`src/Application/Contingency/`) | Compara la antigüedad del outbox contra el umbral, escribe el setting, arma la transición |
| `IContingencyMonitorPump` · `ContingencyMonitorPump` | Application / Service | Un tick: corre el monitor, y si hubo transición, hace fan-out del webhook por tenant |
| `ContingencyMonitorWorker : BackgroundService` | Service | Dispara el pump en intervalo |

Config bootstrap `ContingencyMonitor`: `Enabled` (true), `IntervalSeconds`
(60), `ActivationThresholdMinutes` (5). Es tuning de infraestructura, no un
setting runtime — mismo criterio que `Dgii:Resilience`.

## Fuera de alcance

- **Tipo 2** (imposibilidad técnica): comprobantes Serie B, declaración
  manual vía OFV (Modalidad Total/Parcial + descripción), estado
  `contingency_pending`, plazo de 15 días, regularización a 30 días con e-CF
  de reemplazo que van **solo** a la DGII (nunca al receptor). Necesita
  pantalla de declaración en el dashboard y un flujo de emisión aparte.
- **Tipo 3** (caída de la propia DGII): mecánicamente parecido al Tipo 1
  (almacenar y reenviar), pero sin el plazo estricto de 72h y con reportes
  alternos si supera 15 días hábiles.
- Un reloj explícito de "72h desde que se restableció la conexión" con
  alerta si se vence — el reintento sin dar por perdido ya cubre esto en la
  práctica (se reintenta indefinidamente mientras dure la contingencia); un
  reloj + alerta explícitos son un refinamiento posterior si hace falta
  auditarlo puntualmente.
- Historial de contingencias consultable (la OFV de la DGII lo expone; hoy
  solo queda el rastro en `platform_setting_changes` y en los webhooks
  entregados).
