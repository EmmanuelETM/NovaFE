# Despliegue

NovaFE se despliega como **un contenedor** en **Azure Container Apps**, contra
**Neon** (PostgreSQL gestionado) y **Azure Key Vault** (protección de la KEK del
vault de certificados). Ninguna de esas tres piezas está soldada: cada una vive
detrás de una costura y se cambia por configuración.

## Los puntos de swap

| Pieza | Costura | Hoy | Cambiar a otra cosa |
|---|---|---|---|
| Compute | El contenedor (`src/Service/Dockerfile`) | Azure Container Apps | Cualquier runtime de contenedores (App Service, Railway, Fly, Render, k8s). No hay nada específico de ACA en el código salvo `UseForwardedHeaders` (estándar detrás de cualquier proxy) y los probes `/health/*` (estándar). |
| Base de datos | `ConnectionStrings:Default` + `Database:Provider` | Neon | Cualquier PostgreSQL 16+ (Azure DB for PostgreSQL, Crunchy, RDS, Supabase en session mode…). Solo cambia el connection string y `Database:Provider` (`postgres` \| `neon`). |
| Protección de la KEK | `IKeyProtector` + `CertificateVault:Provider` | `azure-key-vault` | `local` (KEK en variable de entorno) o, en el futuro, `aws-kms` / `gcp-kms` — una implementación nueva de `IKeyProtector` + un `case` en `InfrastructureService`. Ver [`certificates.md`](certificates.md). |
| Vault de certificados (dónde vive el PKCS#12) | `ICertificateVault` | `EnvelopeCertificateVault` (ciphertext en Postgres) | HashiCorp Vault, etc. — Fase 2. Ver [`certificates.md`](certificates.md). |

Runbook completo para mover la API a un VPS (`docker compose` + reverse proxy):
[`vps-migration.md`](vps-migration.md).

La idea: si mañana Neon no sirve, se levanta un Postgres en otro lado, se cambia
el connection string, se corre el Job de migraciones y listo. Si Azure Key Vault
molesta, `CertificateVault:Provider=local` con la KEK en un secreto y el sistema
sigue firmando igual (el formato del ciphertext no cambia — la envoltura de la
DEK sí, así que un cambio de proveedor exige re-envolver los secretos existentes;
a 0–15 clientes es un script de una vez).

## Configuración (variables de entorno)

.NET mapea `Section__Key` a `Section:Key`. En Container Apps van como `env` (las
no sensibles) o `secrets` + `secretRef` (las sensibles).

| Variable | Obligatoria | Ejemplo / notas |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | sí | `Production` |
| `ConnectionStrings__Default` | sí (secreto) | Endpoint **directo** de Neon (no `-pooler`). Ver abajo. |
| `Database__Provider` | no | `neon` — sube el connect timeout (cold start) y exige TLS `VerifyFull`. Default `postgres`. |
| `Database__MigrateOnStartup` | no | `false` en producción (default fuera de Development). Las migraciones son un paso aparte. |
| `CertificateVault__Provider` | no | `azure-key-vault` en producción. Default `local`. |
| `CertificateVault__KeyVaultKeyUri` | con `azure-key-vault` | `https://<vault>.vault.azure.net/keys/cert-kek` |
| `CertificateVault__MasterKey` | con `local` | KEK base64 de 32 bytes. `openssl rand -base64 32`. |
| `Security__AdminApiKey` | sí (secreto) | Clave estática de operador (`X-Admin-Key`). Sin ella, los endpoints de operador rechazan todo. |
| `Dgii__EcfBaseUrl` | no | `https://ecf.dgii.gov.do` (default) |
| `Dgii__FcBaseUrl` | no | `https://fc.dgii.gov.do` (default) |
| `Cors__AllowedOrigins__0` | no | Origen del dashboard. |
| `Observability__OtlpEndpoint` | no | Endpoint OTLP; vacío = no se exportan trazas/métricas. |
| `AZURE_CLIENT_ID` | con managed identity user-assigned | El `clientId` de la identidad, para que `DefaultAzureCredential` elija la correcta. Lo pone el Bicep. |
| `RUN_MIGRATIONS_AND_EXIT` | solo el Job | `true` — aplica migraciones + seeds y termina, sin levantar el servidor. |

## Neon

1. **Usar el endpoint directo, no el pooler.** El connection string de Neon viene
   en dos sabores: `ep-xxx.region.aws.neon.tech` (directo) y
   `ep-xxx-pooler.region.aws.neon.tech` (PgBouncer, **modo transaction**). NovaFE
   fija `app.tenant_id` a nivel de **sesión** (`TenantConnectionInterceptor`,
   `DbSession`); con transaction pooling esa variable —y con ella RLS— deja de
   aplicar entre sentencias. **Usar siempre el host directo.** A 0–15 clientes,
   con una o dos réplicas y `Maximum Pool Size=20`, no hace falta pooler.
   Al arrancar, la app avisa fuerte en el log si detecta un host `-pooler`.
2. **Crear el rol de aplicación restringido.** Ver
   [`deploy/sql/001-app-role.sql`](../deploy/sql/001-app-role.sql). El runtime se
   conecta como `novafe_app` (sin `BYPASSRLS`); las migraciones, como el rol
   dueño. Sin esto RLS no "muerde" — es la red de seguridad de producción (ver
   [`multi-tenancy.md`](multi-tenancy.md)).
3. **TLS.** `Database:Provider=neon` fuerza `SSL Mode=VerifyFull` (Neon tiene
   certificados válidos). El connection string puede traerlo explícito igual.
4. **Cold start.** El compute de Neon se suspende tras inactividad; el worker del
   outbox lo mantiene despierto, pero el connect timeout se sube a 30 s por si
   acaso.

## Azure Key Vault (KEK)

`AzureKeyVaultKeyProtector` envuelve/desenvuelve la DEK de cada certificado con
una clave **RSA** del vault (`RSA-OAEP-256`). La operación ocurre en el vault; la
KEK nunca entra en el proceso.

1. El Bicep crea el vault, la clave `cert-kek` y asigna a la identidad de la app
   el rol **Key Vault Crypto User** (puede envolver/desenvolver, no leer ni
   exportar la clave).
2. La credencial la resuelve `DefaultAzureCredential`: la managed identity
   user-assigned de la Container App. En local, `az login`.
3. Rotación de la clave: crear una versión nueva en el vault. `KeyVaultKeyUri`
   apunta a la clave sin versión → usa la más reciente para envolver; las DEK
   viejas se desenvuelven con la versión con que se envolvieron (el vault lo
   resuelve por el token que va en el ciphertext). No hay que re-cifrar nada.

Costo: `RSA-OAEP` wrap/unwrap en tier standard son ~US$0.03 / 10.000 operaciones,
y hay una por operación de certificado (no por e-CF). Centavos al mes.

**Cambiar de proveedor de KEK con certificados ya cargados** exige un paso de
re-envoltura (`CertificateVault:Rewrap:Enabled=true`, un Job de una vez con la
config del proveedor viejo). Detalle en [`certificates.md`](certificates.md). En
un despliegue nuevo no hace falta — es solo config.

## Migraciones

`Database:MigrateOnStartup=false` en producción: con varias réplicas, migrar en
el arranque es una condición de carrera esperando pasar. En su lugar, un
**Container Apps Job** (`<prefix>-migrations`, misma imagen) corre con
`RUN_MIGRATIONS_AND_EXIT=true` **antes** de enrutar tráfico a la revisión nueva.
El advisory lock de `DatabaseInitializer` hace segura la ejecución concurrente.

Flujo de release:
1. Build + push de la imagen nueva.
2. `az containerapp job start -n <prefix>-migrations -g <rg>` → esperar a que
   termine con éxito.
3. `az containerapp update -n <prefix>-api -g <rg> --image <nueva imagen>`.
4. Verificar `GET /health/ready` = 200 en la revisión nueva.

Si la migración nueva no es compatible hacia atrás con la imagen vieja, hay una
ventana durante el paso 2–3. A esta escala se acepta; si molesta, se hace
expand/contract (migración aditiva primero, deploy, migración destructiva
después).

## Health checks → probes de ACA

| Probe | Endpoint | Falla ⇒ |
|---|---|---|
| Liveness | `/health/live` | Reinicia la réplica. No toca la base. |
| Readiness | `/health/ready` | Saca la réplica del balanceador (no la reinicia). Verifica PostgreSQL. |
| Startup | `/health/ready` | Da margen al arranque (migraciones ya corrieron aparte, así que es rápido). |

Los define el Bicep. El `HEALTHCHECK` del Dockerfile es para `docker compose`
local; ACA usa sus propios probes.

## Costos aproximados (0–15 clientes)

| Pieza | US$/mes |
|---|---|
| Container Apps, 1 réplica 0.5 vCPU / 1 GiB always-on | ~10–18 (el grant gratis cubre requests y buena parte del compute; el resto es el replica caliente) |
| Neon | 0 (free) → 19 (Launch, al superar 0.5 GB o querer sin autosuspend) |
| Key Vault | ~1–3 |
| Log Analytics | ~0–5 (retención 30 días, volumen bajo) |

El `minReplicas: 1` es obligatorio: `EcfSubmissionWorker` hace polling del outbox
y no puede escalar a cero. Si algún día se quiere el bill casi en cero, se separa
el worker en un Container Apps Job con trigger cron y la API escala a cero (con el
costo de cold starts en el primer request tras inactividad — malo para el
fast-path de 8 s contra la DGII).

## Rollback

`az containerapp revision list -n <prefix>-api -g <rg>` → activar la revisión
anterior con `az containerapp revision activate`. Si la migración nueva rompió
algo, hay que revertirla a mano (no hay down-migrations automáticas en el Job).

## Runbook detallado

Comandos paso a paso: [`deploy/README.md`](../deploy/README.md).
