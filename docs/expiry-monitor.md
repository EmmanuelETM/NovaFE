# Monitor de vencimientos (RF-01.6)

Un `BackgroundService` (`ExpiryMonitorWorker`) que cada
`ExpiryMonitor:IntervalHours` (6 por defecto) barre los certificados y las
secuencias de cada contribuyente activo y emite un **webhook** cuando alguno
cruza un umbral. Un contribuyente cuyo certificado vence en silencio no puede
facturar; esto lo avisa con antelación.

## Eventos

| Evento | Se dispara cuando | Umbrales |
|---|---|---|
| `certificate.expiring` | El certificado activo se acerca a `ValidTo` | 90 / 30 / 15 / 7 días |
| `certificate.expired` | `now ≥ ValidTo` | — |
| `sequence.expiring` | El rango se acerca a `ExpiresOn` (31-dic del año siguiente a la autorización) | 30 / 7 días |
| `sequence.expired` | `hoy > ExpiresOn` (calendario dominicano) | — |
| `sequence.low` | El stock cayó al 20 % o menos del rango (RF-07.3, `NcfSequence.IsLowStock`) | — |
| `sequence.exhausted` | No quedan secuencias por entregar | — |

El `data.object` del sobre es el recurso tal cual lo devuelve su `GET`
(`CertificateDto` / `NcfSequenceDto`); el consumidor lee `validTo` / `expiresOn` /
`remaining`. Ver [`webhooks.md`](webhooks.md).

## Idempotencia

Cada aviso concreto sale **una sola vez**. La tabla de sistema
`expiry_notifications` (`(subject_type, subject_id, kind)` único, sin RLS) lleva
el registro; `kind` distingue el umbral (`expiring:30`, `expired`, `low`,
`exhausted`). El `INSERT … ON CONFLICT DO NOTHING` hace que solo el primero en
registrar un aviso lo envíe, aun con varios workers. El registro y el encolado
del webhook van en la misma transacción: un fallo al encolar no deja el aviso
marcado como enviado.

Cuando un certificado con `daysLeft = 20` se ve por primera vez, cruza los
umbrales 90 y 30 a la vez → se registran ambos, sale **un** webhook. Después, al
cruzar 15 y 7, sale uno por cada uno.

## Piezas

| Pieza | Capa | Rol |
|---|---|---|
| `ExpiryScan` | Application | Revisa certificados y secuencias del tenant en curso; decide qué avisar |
| `IExpiryNotificationLog` · `PostgresExpiryNotificationLog` | Application / Infrastructure | Registro idempotente de avisos emitidos |
| `IExpiryMonitorPump` · `ExpiryMonitorPump` | Application / Service | Un barrido: lista los tenants activos, revisa cada uno en su scope con el tenant fijado |
| `ExpiryMonitorWorker : BackgroundService` | Service | Dispara el pump en intervalo |

Reusa `ICertificateReadRepository` / `INcfSequenceReadRepository` (lecturas
Dapper, acotadas al tenant) y `IWebhookOutbox` (fan-out a los endpoints
suscritos). Config: `ExpiryMonitor:Enabled` (true), `ExpiryMonitor:IntervalHours` (6).

## Fuera de alcance

- Otros canales de aviso (correo, panel). Hoy solo webhook; un contribuyente sin
  endpoint suscrito no recibe el aviso (el `kind` igual queda registrado).
- Umbrales configurables por contribuyente.
