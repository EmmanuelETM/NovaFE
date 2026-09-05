# Autenticación de la API (Módulo 14)

**Estado: implementado (API keys + ambiente + rol en la key).** La API distingue
dos audiencias y las autentica por separado. Auditoría inmutable (RF-14.4) es un
slice posterior de M14.

| Audiencia | Recursos | Credencial |
|---|---|---|
| **Cliente** (contribuyente) | `POST /api/v1/ecf`, secuencias, certificados, `GET /dgii/connection` | API key — header `X-API-Key` |
| **Operador** del SaaS | `/api/v1/tenants/**` (alta de contribuyentes, perfil de emisor, API keys) | Clave estática — header `X-Admin-Key` contra `Security:AdminApiKey` |

Health, `/openapi`, `/scalar` quedan anónimos. Los controllers `dev/**`
(`SandboxController`, `EcfPreviewController`) solo existen en Development.

---

## API keys de cliente

- **Formato del token**: `sk_nfe_<test|cert|prod>_` + 43 caracteres base64url
  (32 bytes de RNG). El segmento de ambiente hace evidente de un vistazo con qué
  key trabajas. Se enseña **una sola vez**, al crearlo. La base guarda solo su
  `SHA-256` (hex) — que es también la clave de búsqueda O(1) al autenticar — más
  un prefijo en claro para reconocerlo en un listado.
- **Entidad** `ApiKey` (`src/Domain/Tenants/ApiKey.cs`) — pertenece a un `Tenant`,
  la administra el operador (como `EmitterProfile`: **no** es `ITenantOwned`,
  **no** lleva RLS). Campos: `KeyHash`, `Prefix`, `Label`, `Environment`, `Role`,
  `ExpiresAt?`, `RevokedAt?`, `LastUsedAt?`. Tabla `api_keys`.
- **El tenant y el ambiente salen de la key.** El handler
  `ApiKeyAuthenticationHandler` publica un principal con los claims `tenant_id` y
  `dgii_environment`; `TenantResolutionMiddleware` (que ahora corre **después** de
  `UseAuthorization`) los pasa a `ICurrentTenant.TenantId` / `.Environment`.
  `IssueEcfUseCase` toma el ambiente de ahí — el payload de `POST /ecf` **ya no
  lleva `environment`**. La key **es** el selector: es imposible emitir en
  producción con una key de test, o al revés.
- **Ambiente por defecto**: si `POST /tenants/{id}/api-keys` no trae
  `environment`, se usa el `EmitterProfile.DefaultEnvironment`.
- **Guardrail al acuñar**: solo se crea una key para un ambiente si el
  contribuyente ya puede facturar ahí — perfil de emisor + certificado activo +
  algún rango de secuencia e-NCF. Si no, `400`
  (`ApiKey.EnvironmentNotReady`) con el detalle de lo que falta. El orden normal
  del onboarding (perfil → cert → secuencias → key) lo satisface.
- **`X-Tenant-Id` sin credencial solo funciona en Development** (esquema
  `DevTenantHeader`, registrado solo ahí) — para el sandbox, el
  `NovaFE.Service.http` y las pruebas de integración. Ahí el ambiente cae al
  `DefaultEnvironment` del perfil. En producción el único camino es la API key.
- **Fuerza bruta** (RF-14.6): `IApiKeyThrottle` cuenta los intentos fallidos por
  IP; 5 en 5 minutos → bloqueo de 15 (respuesta `401`, fail-closed). En memoria;
  si hubiera varias instancias pasaría a la caché distribuida.
- **`LastUsedAt`**: lo escribe el autenticador best-effort, coalescido a 1×/5 min
  por key. Un fallo al escribirlo no niega el acceso.

## RBAC (RF-14.5)

El rol también vive en la API key — no hay usuarios/login todavía, así que el
permiso se asigna por credencial, igual que el ambiente. `ApiKeyRole`
(`src/Domain/Tenants/ApiKeyRole.cs`) tiene los 3 roles de contribuyente del Plan
Técnico (`admin_sistema`, el cuarto rol, es exclusivo del operador y usa otro
esquema de auth por completo):

| Rol | Puede |
|---|---|
| `admin_tenant` | Certificados, secuencias, conexión DGII (`TenantConfig`) |
| `emisor` | Emitir/reencolar e-CF (`EcfIssue`) + todo lo de `consultor` |
| `consultor` | Consultar comprobantes y su estado (`EcfRead`) |

- **Al acuñar una key el `role` es obligatorio** — a diferencia del ambiente, no
  hay default: `POST .../api-keys` sin `role` es `400`. `admin_sistema` no es un
  valor válido aquí (es del operador).
- El handler de API key publica el rol como `ClaimTypes.Role`; las 3 políticas
  (`TenantConfig`, `EcfIssue`, `EcfRead` en `SecuritySchemes.cs`) exigen tenant +
  uno de los roles permitidos vía `RequireRole(...)`. `CertificatesController`,
  `SequencesController` y `DgiiController` usan `TenantConfig` a nivel de clase;
  `EcfController` mezcla `EcfIssue` (emitir, `retry`) y `EcfRead` (el resto) por
  acción.
- El camino `X-Tenant-Id` de Development (`DevTenantHeaderAuthenticationHandler`)
  publica siempre `admin_tenant` — es un atajo de confianza que solo existe ahí,
  no tiene sentido replicar el RBAC de las keys reales.
- Para cambiar el rol de una key existente: se revoca y se acuña otra (mismo
  patrón que cambiar de ambiente).

### Endpoints (operador)

```
POST   /api/v1/tenants/{id}/api-keys            → 201 { key: {...}, token: "sk_nfe_test_…" }
GET    /api/v1/tenants/{id}/api-keys            → 200 [ { id, prefix, label, environment, … } ]  (sin tokens)
DELETE /api/v1/tenants/{id}/api-keys/{keyid}    → 204                                            (revoca; deja de autenticar ya)
```

Cuerpo del `POST` (`role` obligatorio, el resto opcional):
`{ "label": "ERP contable", "environment": "Production", "role": "emisor", "expiresAt": "2027-01-01T00:00:00Z" }`.
Para cambiar el ambiente o el rol de una key se revoca y se acuña otra.

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
contribuyente recién creado, en el ambiente del sandbox). Úsala como `X-API-Key`,
o sigue usando `tenantId` como `X-Tenant-Id`. Ver `docs/local-e2e.md`.

## Fuera de alcance (slices/módulos posteriores)

- **Auditoría inmutable** append-only de RF-14.4.
- **Auth de operador con usuarios** (login, panel) — hoy sigue siendo una sola
  clave estática compartida por todo operador; RBAC de operador de verdad
  necesita eso primero.
- **Rate limiting por plan** (RF-12.3) — el limiter global ya particiona por
  tenant (el `Name` del principal); los topes por plan son otro slice.
- **Scopes por key** (más finos que el rol), rotación con período de gracia,
  cambiar el ambiente o el rol de una key existente, y el `X-API-Key` en los
  endpoints B2B (esos van por certificado, y son el Módulo 5).
