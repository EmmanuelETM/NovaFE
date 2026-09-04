# Autenticación de la API (Módulo 14 — slice 1)

**Estado: implementado (API keys).** La API distingue dos audiencias y las
autentica por separado. RBAC por roles (RF-14.5) y auditoría inmutable (RF-14.4)
son slices posteriores de M14.

| Audiencia | Recursos | Credencial |
|---|---|---|
| **Cliente** (contribuyente) | `POST /api/v1/ecf`, secuencias, certificados, `GET /dgii/connection` | API key — header `X-API-Key` |
| **Operador** del SaaS | `/api/v1/tenants/**` (alta de contribuyentes, perfil de emisor, API keys) | Clave estática — header `X-Admin-Key` contra `Security:AdminApiKey` |

Health, `/openapi`, `/scalar` quedan anónimos. Los controllers `dev/**`
(`SandboxController`, `EcfPreviewController`) solo existen en Development.

---

## API keys de cliente

- **Formato del token**: `nfe_` + 43 caracteres base64url (32 bytes de RNG). Se
  enseña **una sola vez**, al crearlo. La base guarda solo su `SHA-256` (hex) —
  que es también la clave de búsqueda O(1) al autenticar — más un prefijo en claro
  para reconocerlo en un listado.
- **Entidad** `ApiKey` (`src/Domain/Tenants/ApiKey.cs`) — pertenece a un `Tenant`,
  la administra el operador (como `EmitterProfile`: **no** es `ITenantOwned`,
  **no** lleva RLS). Campos: `KeyHash`, `Prefix`, `Label`, `ExpiresAt?`,
  `RevokedAt?`, `LastUsedAt?`. Tabla `api_keys`.
- **El tenant sale de la key.** El handler `ApiKeyAuthenticationHandler` publica un
  principal con el claim `tenant_id`; `TenantResolutionMiddleware` (que ahora corre
  **después** de `UseAuthorization`) lo pasa a `ICurrentTenant`. Los casos de uso
  **no cambiaron**.
- **`X-Tenant-Id` sin credencial solo funciona en Development** (esquema
  `DevTenantHeader`, registrado solo ahí) — para el sandbox, el
  `NovaFE.Service.http` y las pruebas de integración. En producción el único
  camino es la API key.
- **Fuerza bruta** (RF-14.6): `IApiKeyThrottle` cuenta los intentos fallidos por
  IP; 5 en 5 minutos → bloqueo de 15 (respuesta `401`, fail-closed). En memoria;
  si hubiera varias instancias pasaría a la caché distribuida.
- **`LastUsedAt`**: lo escribe el autenticador best-effort, coalescido a 1×/5 min
  por key. Un fallo al escribirlo no niega el acceso.

### Endpoints (operador)

```
POST   /api/v1/tenants/{id}/api-keys            → 201 { key: {...}, token: "nfe_…" }
GET    /api/v1/tenants/{id}/api-keys            → 200 [ { id, prefix, label, … } ]   (sin tokens)
DELETE /api/v1/tenants/{id}/api-keys/{keyid}    → 204                                (revoca; deja de autenticar ya)
```

Cuerpo del `POST` (todo opcional): `{ "label": "ERP contable", "expiresAt": "2027-01-01T00:00:00Z" }`.

## Clave de operador

- `Security:AdminApiKey` en configuración (env `Security__AdminApiKey` en
  producción). Comparación en tiempo constante.
- **Sin configurar**: en Development los endpoints de operador quedan **abiertos**
  con un aviso en el log; fuera de Development el handler **rechaza todo** y
  `Program.cs` avisa al arrancar.
- No hay usuarios ni roles todavía: una sola clave para todo el operador. La auth
  de operador "de verdad" (usuarios, panel, RBAC) es otro módulo.

## Onboarding local

`POST /api/v1/dev/sandbox` ahora devuelve también `apiKey` (una key `Sandbox` del
contribuyente recién creado). Úsala como `X-API-Key`, o sigue usando `tenantId`
como `X-Tenant-Id`. Ver `docs/local-e2e.md`.

## Fuera de alcance (slices/módulos posteriores)

- **RBAC** de los 4 roles de RF-14.5 (`admin_tenant`, `emisor`, `consultor`,
  `admin_sistema`).
- **Auditoría inmutable** append-only de RF-14.4.
- **Auth de operador con usuarios** (login, panel).
- **Rate limiting por plan** (RF-12.3) — el limiter global ya particiona por
  tenant (el `Name` del principal); los topes por plan son otro slice.
- **Scopes por key**, rotación con período de gracia, y el `X-API-Key` en los
  endpoints B2B (esos van por certificado, y son el Módulo 5).
