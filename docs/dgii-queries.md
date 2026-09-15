# Consultas a la DGII (Módulo 10)

Los tres servicios de consulta que quedaban pendientes de M10
(`consultaresultado` ya se usa en Módulo 4, ver `docs/dgii-submission.md`):
**trackIds**, **directorio**, y **estatus de servicio**. No viven en el mismo
sitio ni con la misma confianza documental — ver la tabla de abajo antes de
tocar nada.

| Servicio | Verificación | Dominio | Auth |
|---|---|---|---|
| `consultatrackids` | ✅ Path, método, request y response exactos (Descripción Técnica de Servicios DGII v1.7 §G) | `ecf.dgii.gov.do` (el mismo de siempre) | Bearer, el mismo `IDgiiTokenProvider` |
| `consultadirectorio` | ✅ Path, método, request y response exactos (§J) | `ecf.dgii.gov.do` | Bearer |
| Estatus de servicio | ⚠️ Path, método y esquema de auth verificados contra el OpenAPI público del servicio (`https://statusecf.dgii.gov.do/api-docs/v1/definition.json`) — **el schema de la respuesta no está documentado**, cada endpoint solo dice `"200": {"description": "Success"}` | `statusecf.dgii.gov.do` (dominio propio) | `Authorization: ApiKey {key}` — **no Bearer**, API key estática que la DGII entrega al iniciar la integración técnica |

Ambos servicios de `ecf.dgii.gov.do` **no están disponibles en CerteCF** —
solo TesteCF y Producción. `IDgiiQueryClient` devuelve
`DgiiQueryErrors.NotAvailableInEnvironment` sin llamar si se pide en Cert.

## trackIds — `IDgiiQueryClient.GetTrackIdsAsync`

`GET {ecfBase}/{amb}/consultatrackids/api/trackids/consulta?rncemisor=X&encf=Y`
→ lista de `{trackId, estado, fechaRecepcion}` — puede haber más de una entrada
si el e-NCF se remitió varias veces (`issued_ecf.track_id` solo guarda la
última). Expuesto como `GET /api/v1/ecf/{id}/trackids` en `EcfController`
(política `EcfRead`, mismo grupo que `GetById`/`GetXml`), vía
`GetEcfTrackIdsUseCase` — resuelve el `IssuedEcf` (para `Environment` y
`Encf`), el RNC del tenant (`ITenantRepository`) y pide un token con
`IDgiiTokenProvider.GetTokenAsync` antes de llamar al cliente.

## Directorio — `IDgiiQueryClient.ListDirectoryAsync` / `GetDirectoryEntryAsync`

`GET .../consultadirectorio/api/consultas/listado` (todos) o
`.../obtenerDirectorioporrnc?RNC=X` (uno) → `{rnc, nombre, urlRecepcion,
urlAceptacion, urlOpcional}` — la URL B2B de un contribuyente para enviarle un
e-CF directamente. **Sin consumidor real hoy**: lo necesita el Módulo 5
(receptor electrónico B2B), sin construir. El cliente está listo; no hay
endpoint propio todavía — no tiene sentido exponerlo sin quien lo llame.

## Estatus de servicio — `IDgiiStatusClient`

Tres endpoints, todos `GET`, sin body, confirmados contra el OpenAPI público
del servicio:

```
GET /api/EstatusServicios/ObtenerEstatus                    → lista de servicios + disponibilidad
GET /api/EstatusServicios/ObtenerVentanasMantenimiento       → calendario de mantenimientos
GET /api/EstatusServicios/VerificarEstado?Ambiente={1|2|3}   → ¿está en ventana de mantenimiento este ambiente?
```

`Ambiente` es un enum `[1,2,3]` sin etiquetar en el spec — coincide con
`DgiiEnvironment.Id` que ya usa el dominio (`Test=1, Cert=2, Production=3`),
así que `DgiiStatusClient` manda `environment.Id` directamente. Coincide con
el orden T/C/P que usa el resto de la documentación de la DGII, pero no está
confirmado contra un ambiente real — si alguna vez sale mal, es el primer
sospechoso.

**Por qué el parseo es defensivo.** Como ningún endpoint documenta el schema
de la respuesta, `DgiiServiceStatus` / `DgiiMaintenanceWindow` /
`DgiiEnvironmentStatus` (`src/Application/Dgii/Contracts/DgiiStatusContracts.cs`)
tienen todos los campos opcionales, se buscan con varios nombres alternativos
razonables (`nombre`/`servicio`/`name`, `disponible`/`activo`/`estado`, etc.,
case-insensitive), y **siempre** guardan el JSON crudo en `RawJson` — si
ningún campo esperado matchea, igual queda algo que un humano puede mirar.
Un array envuelto en un objeto (`{"items": [...]}`) también se tolera
(`DgiiStatusClient` busca la primera propiedad que sea un array). Un
`JsonException` se mapea a `DgiiQueryErrors.MalformedResponse`, nunca
revienta.

**No conectado a ninguna decisión automática.** Ni a `ContingencyMonitor`
(`docs/contingency.md` — que ya resuelve la detección de M11 Tipo 1 con la
antigüedad del outbox, sin depender de este servicio) ni a ninguna otra
lógica. Sale solo como diagnóstico de operador:
`GET /api/v1/ops/dgii-status` (`OpsController`, misma política que
`/ops/status`), que llama a los tres endpoints — un fallo en uno no tumba los
otros dos — y devuelve `verified: false` siempre, como recordatorio de que
esto es lectura tolerante, no un contrato confirmado.

**Siguiente paso natural, no construido todavía**: una vez que haya una
`Dgii:StatusApiKey` real y se pueda confirmar el schema contra TesteCF, decidir
si conectar `VerificarEstado`/`ObtenerVentanasMantenimiento` a
`ContingencyMonitor` como señal **proactiva** (detectar una ventana de
mantenimiento anunciada *antes* de que el outbox se estanque) tiene sentido.
Hoy no se sabe si el campo que hoy se adivina como `enMantenimiento` es
siquiera el correcto.

## Configuración

`DgiiOptions` (`src/Infrastructure/Dgii/DgiiOptions.cs`):

| Clave | Default | |
|---|---|---|
| `Dgii:StatusBaseUrl` | `https://statusecf.dgii.gov.do` | Sin segmento de ambiente — el dominio de estatus no lo lleva. |
| `Dgii:StatusApiKey` | `""` | Vacío deshabilita el cliente (`DgiiQueryErrors.StatusApiKeyNotConfigured`) en vez de intentar la llamada. Nunca en el repo — variable de entorno o user-secrets. |
| `Dgii:StatusTimeoutSeconds` | `30` | Timeout total del cliente `dgii-status`. |

El cliente resiliente `dgii-status` usa la misma sección `Dgii:Resilience`
(circuit breaker) que `dgii-auth`/`dgii-ecf`/`dgii-fc` — ver
`docs/dgii-submission.md` §Resiliencia.

`deploy/main.bicep` (Azure) todavía no tiene `Dgii:StatusApiKey`/
`StatusBaseUrl` cableados — igual que `Security:InternalApiKey`, que tampoco
está ahí. Es deuda ya existente del template, no algo nuevo de este slice.

## Fuera de alcance

- `consultaestado` (receptor, requiere estar "delegado" para el emisor o
  comprador) y `consultarfce` (equivalente de `consultaresultado` para el
  RFCE) — no los pide el roadmap para este slice.
- Endpoint público para el directorio, y su caché (Redis, TTL 24h, que
  menciona el Plan Técnico) — sin consumidor real todavía (M5).
- Conectar el estatus de servicio a `ContingencyMonitor` o a cualquier
  decisión automática — depende de verificar el schema real primero (ver
  arriba).
