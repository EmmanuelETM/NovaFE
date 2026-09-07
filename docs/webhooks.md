# Webhooks (RF-12.7)

> **Estado: implementado.** Los 6 eventos del ciclo de vida del e-CF + los 6 de
> vencimiento de certificados y secuencias (RF-01.6, `docs/expiry-monitor.md`),
> con suscripciones + outbox de entrega + firma HMAC + log de entregas. Los de
> M5/M8/M11 quedan para su módulo (§Fuera de alcance).

Notificaciones asíncronas al ERP del cliente para no depender del polling de
`GET /ecf/{id}`. Cada tenant configura uno o más endpoints, elige a qué eventos se
suscribe, y NovaFE le entrega un `POST` firmado (HMAC-SHA256) cada vez que ocurre
uno.

La entrega es **at-least-once** sobre un outbox en PostgreSQL — el mismo patrón
que el envío a la DGII (`docs/dgii-submission.md`): sin broker, con reintento y
backoff, atómico con la transición de estado que lo origina.

Fuente: `Plan Técnico Integral v2.0` RF-12.7. Formato del sobre y de la firma:
convención de Stripe / GitHub.

---

## Eventos

Tipo discreto `categoria.evento`. El cliente se suscribe a tipos exactos o a un
comodín (`ecf.*`, `*`).

### Ciclo de vida del e-CF

| Evento | Se dispara cuando | `data.object.status` |
|---|---|---|
| `ecf.submitted` | La DGII recibió el comprobante y devolvió `TrackId` | `submitted` |
| `ecf.accepted` | La DGII aceptó (código 1) — validez fiscal | `accepted` |
| `ecf.accepted_conditional` | Aceptado con observación (código 4) — tiene validez | `accepted_conditional` |
| `ecf.rejected` | La DGII rechazó (código 2) — nulidad | `rejected` |
| `ecf.review` | Polling agotado sin resolución — revisión manual | `review` |
| `ecf.failed` | Falló el transporte tras agotar los reintentos | `failed` |

Se emiten desde `EcfSubmissionProcessor`, **en la misma transacción** que la
transición del agregado (`IssuedEcf.Mark*`).

`ecf.signed` **no** existe: es el resultado síncrono del `POST /ecf`, el cliente
ya lo tiene en la respuesta. `webhook.ping` es un evento sintético para probar un
endpoint (`POST /webhooks/{id}/ping`).

### Vencimientos (RF-01.6 — ver [`expiry-monitor.md`](expiry-monitor.md))

| Evento | Se dispara cuando |
|---|---|
| `certificate.expiring` | El certificado activo se acerca a `ValidTo` (90 / 30 / 15 / 7 días) |
| `certificate.expired` | El certificado venció |
| `sequence.expiring` | El rango se acerca a `ExpiresOn` (30 / 7 días) |
| `sequence.expired` | El rango venció |
| `sequence.low` | El stock cayó al 20 % o menos del rango |
| `sequence.exhausted` | No quedan secuencias por entregar |

Los emite `ExpiryMonitorWorker` en un barrido periódico. El `data.object` es el
`CertificateDto` / `NcfSequenceDto` tal cual su `GET`.

### Después (llegan con su módulo)

| Evento | Módulo |
|---|---|
| `inbound_ecf.received` · `acknowledgement.received` · `commercial_approval.received` | M5 |
| `ecf.voided` | M8 |
| `contingency.activated` · `contingency.deactivated` | M11 |

## El sobre (envelope)

```json
{
  "id": "evt_018f3c2a-...",
  "object": "event",
  "type": "ecf.accepted",
  "apiVersion": "1",
  "createdAt": "2026-09-07T14:03:11-04:00",
  "data": {
    "object": { /* el e-CF, idéntico a GET /api/v1/ecf/{id} */ }
  }
}
```

- `id` — único por evento (UUIDv7 con prefijo `evt_`). **Es la clave de
  idempotencia**: se repite en cada reintento y para cada endpoint suscrito.
- `object` — siempre `"event"`.
- `apiVersion` — `"1"`.
- `createdAt` — hora dominicana (UTC-4), como todo lo que emite la API.
- `data.object` — el recurso, con el mismo shape que el `GET` correspondiente.
  Para los eventos de e-CF, incluye además `data.previousStatus` cuando aplica.

## Firma y headers

Cada entrega es un `POST` con estos headers:

| Header | |
|---|---|
| `Content-Type` | `application/json` |
| `User-Agent` | `NovaFE-Webhooks/1` |
| `X-NovaFE-Event` | el `type` (`ecf.accepted`) |
| `X-NovaFE-Delivery` | id del intento de entrega — estable entre reintentos al mismo endpoint |
| `X-NovaFE-Timestamp` | Unix segundos del envío |
| `X-NovaFE-Signature` | `sha256=<hex>` |

`<hex>` = `HMAC_SHA256(secret, "{X-NovaFE-Timestamp}.{cuerpo crudo}")`.

**Verificación en el consumidor:**
1. Rechazar si `|ahora − X-NovaFE-Timestamp| > 300s` (anti-replay).
2. Recomputar el HMAC sobre `"{timestamp}.{body}"` con el secret del endpoint.
3. Comparar en tiempo constante contra `X-NovaFE-Signature`.

El `secret` se entrega **una sola vez**, en la respuesta del `POST /webhooks` (y
del `rotate-secret`). NovaFE lo guarda en claro (lo necesita para firmar); es un
secreto compartido de bajo valor y rotarlo es una llamada.

## Entrega

- **Outbox `webhook_deliveries`** (tabla de sistema, sin RLS; lleva `tenant_id`
  en la fila). Al ocurrir un evento, `IWebhookEventQueue` hace *fan-out*: una fila
  por cada endpoint del tenant, habilitado y suscrito a ese tipo. Si no hay
  ninguno, es un no-op.
- **`WebhookDeliveryWorker`** (`BackgroundService`) dispara un pump en intervalo;
  reclamo `FOR UPDATE SKIP LOCKED` + `locked_by` por llamada, igual que el envío a
  la DGII. Multi-instancia seguro.
- **`WebhookDeliveryProcessor`** entrega una fila: `2xx` → `delivered`; cualquier
  otra cosa (incluye timeout y error de conexión) → reprograma con backoff.
- **Backoff** (config `Webhooks:BackoffLadder`): `10s, 1m, 5m, 30m, 2h, 6h`.
  Tras `Webhooks:MaxAttempts` (7) → `dead`. Un endpoint con
  `Webhooks:AutoDisableAfterConsecutiveFailures` (20) fallos seguidos se
  deshabilita solo (el cliente lo re-habilita con `PATCH`).
- **Timeout** de cada `POST`: `Webhooks:DeliveryTimeoutSeconds` (10). La
  resiliencia de reintento la da el outbox, no Polly.
- Las filas `delivered` / `dead` son el **log de entregas**; se purgan tras
  `Webhooks:DeliveriesRetentionDays` (30).

## Suscripciones

Recurso por tenant, política `TenantConfig` (rol `admin_tenant`).

| Método | Ruta | |
|---|---|---|
| `POST` | `/api/v1/webhooks` | `{ url, events[], description? }` → `201 { id, secret, ... }` (el `secret` solo acá) |
| `GET` | `/api/v1/webhooks` | Lista (sin `secret`) |
| `GET` | `/api/v1/webhooks/{id}` | Uno (sin `secret`) |
| `PATCH` | `/api/v1/webhooks/{id}` | `{ url?, events?, enabled?, description? }` |
| `POST` | `/api/v1/webhooks/{id}/rotate-secret` | → `200 { secret }` (el viejo deja de valer al instante) |
| `POST` | `/api/v1/webhooks/{id}/ping` | Entrega un `webhook.ping` **inline** (no pasa por el outbox) y devuelve `{ delivered, statusCode, error }` |
| `DELETE` | `/api/v1/webhooks/{id}` | Borrado lógico; deja de recibir entregas de inmediato |
| `GET` | `/api/v1/webhooks/{id}/deliveries` | Log paginado (`?page=&pageSize=`): tipo, `status`, `lastStatusCode`, `attempts`, timestamps |

**Validación del `url`** (`WebhookEndpoint.Create` + validador):
- `https` obligatorio fuera de Development.
- URI absoluta.
- **Guard anti-SSRF**: se rechaza si resuelve a loopback, IP privada
  (RFC 1918), link-local (`169.254.0.0/16`, incluye el endpoint de metadata de
  la nube) o `::1`. Se re-verifica en cada entrega (DNS rebinding).
- Máximo `Webhooks:MaxEndpointsPerTenant` (5) por tenant.

`events` acepta tipos exactos y comodines (`ecf.*`, `*`). Un tipo desconocido en
la lista → `400`.

## Idempotencia y orden

- **At-least-once**: un evento puede llegar más de una vez (reintento tras un
  `2xx` que no llegó a registrarse, redeploy a mitad de entrega). El consumidor
  **debe** deduplicar por el `id` del sobre.
- **Sin garantía de orden**: `ecf.accepted` puede llegar antes que
  `ecf.submitted`. El consumidor decide por `data.object.status`, no por el orden
  de llegada.

## Piezas

| Interfaz (Application) | Impl | Rol |
|---|---|---|
| `IWebhookEndpointRepository` · `IWebhookEndpointReadRepository` · `IWebhookDeliveryReadRepository` | EF / Dapper | CRUD + log (`webhook_endpoints` `ITenantOwned`; `webhook_deliveries` sistema) |
| `IWebhookUrlPolicy` | `HttpWebhookUrlPolicy` | https + guard anti-SSRF (`PrivateAddressGuard` + DNS) |
| `IWebhookOutbox` | `PostgresWebhookOutbox` | Fan-out del evento, claim/reschedule/dead/reap/purge |
| `IWebhookSignature` | `HmacWebhookSignature` | El `X-NovaFE-Signature` |
| `IWebhookSender` | `HttpWebhookSender` | El `POST` firmado; re-corre el guard antes de conectar |
| `WebhookDeliveryProcessor` · `IWebhookDeliveryPump` | `WebhookDeliveryPump` + `WebhookDeliveryWorker` (Service) | Un tick: reap + claim + entregar; purga en los ticks vacíos |
| `WebhookEvent` (Application) | — | Construye el sobre |

Los eventos del e-CF los emite `EcfSubmissionProcessor` en la **misma transacción**
que la transición `IssuedEcf.Mark*` (`PersistAndNotifyAsync`), reusando
`EcfDtoAssembler.From(ecf)` para el `data.object`.

Config `Webhooks`: `Enabled` (true), `MaxEndpointsPerTenant` (5),
`RequireHttps` (true), `DeliveryTimeoutSeconds` (10), `MaxAttempts` (7),
`AutoDisableAfterConsecutiveFailures` (20), `DeliveriesRetentionDays` (30).

## Fuera de alcance (v1)

- Los eventos de M5 / M8 / M11 (llegan con su módulo).
- Rotación de secret con período de gracia (v1: el viejo muere al instante).
- Reintento manual de una entrega `dead` desde la API.
- Filtros de payload / transformaciones por endpoint.
- Firma asimétrica (hoy HMAC simétrico; el secret alcanza a esta escala).
