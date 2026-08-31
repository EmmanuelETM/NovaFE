# Autenticación con la DGII

Todos los servicios de la DGII piden `Authorization: Bearer {token}`. El token se
obtiene con un flujo de semilla y vale ~1 hora.

Fuente: `C:\workplace\FE_DGII\contexto-proyecto-fe-dgii.md` §5.1 y §5.10.

## El flujo

```
GET  {EcfBaseUrl}/{ambiente}/autenticacion/api/autenticacion/semilla   → XML semilla
      ↓ firmar con el certificado del tenant (IXmlSigner, docs/signing.md)
POST {EcfBaseUrl}/{ambiente}/autenticacion/api/autenticacion/validarsemilla
      (multipart/form-data, campo "xml" = semilla firmada)              → JSON { token, expira, expedido }
```

`{ambiente}` es `testecf` / `certecf` / `ecf` (`DgiiEnvironment.UrlSegment`).

## Piezas

| Interfaz (Application) | Impl (Infrastructure) | Rol |
|---|---|---|
| `IDgiiAuthClient` | `DgiiAuthClient` | HTTP puro: `GetSeedAsync`, `ValidateSeedAsync`. Cliente con resiliencia (`AddResilientHttpClient`: reintentos, circuit breaker, timeouts). Los fallos de red → `Errors.Http.*` vía `HttpErrorMapper`. |
| `IDgiiTokenCache` | `DistributedCacheDgiiTokenCache` | Caché por `(tenant, ambiente)` sobre `IDistributedCache` (en memoria hoy; Redis igual). La entrada expira sola cuando el token vence. Nunca en base de datos. |
| `IDgiiTokenProvider` | `DgiiTokenProvider` | Orquesta: caché → semilla → firma → validar → guardar. **Renovación proactiva** (RF-01.3): si el token vence dentro de `TokenRenewalBufferMinutes` (default 5), se renueva antes. |

`DgiiTokenGate` (singleton) serializa la renovación por `(tenant, ambiente)`: si
llegan varias peticiones con la caché vacía, solo una corre el flujo; las demás
esperan y toman el resultado.

`AuthenticationToken` (dominio): `Value`, `IssuedAt`, `ExpiresAt`, con
`IsExpired`, `NeedsRenewal(now, buffer)`, `RemainingLifetime`.

## Endpoint

`GET /api/v1/dgii/connection?environment=TestEcf` (por tenant) — fuerza el flujo
y responde `{ connected, environment, issuedAt, expiresAt }`. **No devuelve el
token.** Es lo que el operador usa para verificar el onboarding de un cliente.

## Configuración (`Dgii`)

| Clave | Default | |
|---|---|---|
| `EcfBaseUrl` | `https://ecf.dgii.gov.do` | La `BaseAddress` se resuelve al crear el cliente, así que los tests / variables de entorno la sobreescriben. |
| `TokenRenewalBufferMinutes` | `5` | RF-01.3 |
| `AuthTimeoutSeconds` | `60` | Timeout total del cliente de auth |

## Lo que falta con la DGII real (sin TesteCF no se puede confirmar)

Probado end-to-end contra un WireMock (URLs exactas, semilla firmada en el POST,
parseo del token, caché, renovación, errores). Contra TesteCF hay que verificar:

1. **El formato exacto de la semilla** — no la parseamos (la firmamos tal cual),
   pero su root/estructura podría afectar la firma. `Semilla v.1.0.xsd` está en
   `C:\workplace\FE_DGII\XSD\`.
2. **Que la DGII acepte nuestra semilla firmada** (mismo riesgo de canonicalización
   que en `docs/signing.md`).
3. **Los nombres de campo del token** (`token` / `expira` / `expedido`) y su
   formato de fecha.
4. Si la DGII expone autenticación B2B propia (`/fe/autenticacion/...`), ese es
   otro cliente distinto (lo expone cada contribuyente, no lo consumimos así).
